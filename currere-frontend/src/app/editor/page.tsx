'use client';

import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useRef } from 'react';
import api from '@/services/api';
import axios from 'axios';

import EditorHeader from '@/components/editor/EditorHeader';
import CodeEditor from '@/components/editor/CodeEditor';
import TerminalOutput from '@/components/editor/TerminalOutput';

export default function EditorPage() {
  const { activeWorkspace } = useWorkspaceStore();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  // States for Execution
  const [code, setCode] = useState('print("Merhaba TEKNOFEST 2026!")');
  const [terminalOutput, setTerminalOutput] = useState('');
  const [isExecuting, setIsExecuting] = useState(false);
  const [isError, setIsError] = useState(false);
  
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
    return () => {
      // Cleanup polling on unmount
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
  }, []);

  useEffect(() => {
    // Redirect to dashboard if no active workspace is selected
    if (mounted && !activeWorkspace) {
      router.push('/dashboard');
    }
  }, [mounted, activeWorkspace, router]);

  const handleRun = async () => {
    if (!activeWorkspace) return;
    
    setIsExecuting(true);
    setIsError(false);
    setTerminalOutput('Yürütülüyor...');

    try {
      const response = await api.post(`/execution/${activeWorkspace.id}/run`, { code });
      
      if (response.data && response.data.jobId) {
        const jobId = response.data.jobId;
        // Don't show technical details, just "Yürütülüyor..."
        setTerminalOutput('Yürütülüyor...');
        pollStatus(jobId);
      } else {
        setIsError(true);
        setTerminalOutput('Hata: Beklenmeyen bir yanıt alındı.');
        setIsExecuting(false);
      }
    } catch (err: unknown) {
      handleCatchError(err);
      setIsExecuting(false);
    }
  };

  const pollStatus = (jobId: string) => {
    if (pollingIntervalRef.current) {
      clearInterval(pollingIntervalRef.current);
    }

    pollingIntervalRef.current = setTimeout(() => {
      const intervalId = setInterval(async () => {
        try {
          const response = await api.get(`/execution/status/${jobId}`);
          const data = response.data;
          
          const status = data.status || data.Status;
          const statusStr = typeof status === 'string' ? status.toLowerCase() : '';
          const isSuccess = data.isSuccess !== undefined ? data.isSuccess : data.IsSuccess;

          if (isSuccess !== undefined || statusStr === 'completed' || statusStr === 'failed') {
            clearInterval(intervalId);
            setIsExecuting(false);

            if (isSuccess !== undefined) {
              if (isSuccess) {
                setTerminalOutput(data.output || data.Output || 'Çalıştırma tamamlandı (Çıktı yok).');
              } else {
                setIsError(true);
                setTerminalOutput((data.error || data.Error) + '\n' + (data.errorType || data.ErrorType || ''));
              }
            } else {
              setIsError(statusStr === 'failed');
              setTerminalOutput(statusStr === 'failed' ? 'Çalıştırma başarısız oldu.' : 'Çalıştırma tamamlandı but sonuç alınamadı.');
            }
          }
        } catch (err: unknown) {
          clearInterval(intervalId);
          setIsExecuting(false);
          handleCatchError(err);
        }
      }, 2000);

      // Overwrite the ref with the interval ID so it can be cleaned up on unmount/re-run
      pollingIntervalRef.current = intervalId;
    }, 1500);
  };

  // Dedicated error handler for 400 Bad Request
  const handleCatchError = (err: unknown) => {
    setIsError(true);
    if (axios.isAxiosError(err)) {
      if (err.response?.status === 400) {
        // Backend Validation Error (Syntax Error, etc)
        const errorData = err.response.data;
        const errorMsg = errorData.error || errorData.message || (errorData.errors ? JSON.stringify(errorData.errors) : 'Geçersiz İstek (400)');
        setTerminalOutput(`Çalıştırma Hatası:\n${errorMsg}`);
      } else {
        setTerminalOutput(err.response?.data?.message || 'Hata: Bir problem oluştu.');
      }
    } else {
      setTerminalOutput('Beklenmeyen bir hata oluştu.');
    }
  };

  if (!mounted || !activeWorkspace) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-50">
        <div className="w-8 h-8 border-4 border-zinc-200 border-t-zinc-900 rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <div className="h-screen w-full flex flex-col bg-white text-zinc-900 overflow-hidden font-sans">
      <EditorHeader
        activeWorkspace={activeWorkspace}
        isExecuting={isExecuting}
        onRun={handleRun}
      />
      
      <main className="flex-1 flex overflow-hidden">
        <CodeEditor code={code} setCode={setCode} />
        <TerminalOutput output={terminalOutput} isError={isError} />
      </main>
    </div>
  );
}
