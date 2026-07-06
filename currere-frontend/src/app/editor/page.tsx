'use client';

import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useRef, useCallback } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { useFileCache } from '@/hooks/useFileCache';

import EditorHeader from '@/components/editor/EditorHeader';
import FileExplorer from '@/components/editor/FileExplorer';
import CodeEditor from '@/components/editor/CodeEditor';
import CsvTable from '@/components/editor/CsvTable';
import TerminalOutput from '@/components/editor/TerminalOutput';
import CurrereAI from '@/components/editor/CurrereAI';
import ResizablePanels from '@/components/editor/ResizablePanels';

export default function EditorPage() {
  const { activeWorkspace, activeFile } = useWorkspaceStore();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  // States for Execution
  const [code, setCode] = useState('');
  const [terminalOutput, setTerminalOutput] = useState('');
  const [isExecuting, setIsExecuting] = useState(false);
  const [isError, setIsError] = useState(false);
  const [outputImages, setOutputImages] = useState<string[]>([]);
  const [forceVisualTab, setForceVisualTab] = useState(false);

  // localStorage tabanlı kalıcı dosya cache'i
  const { readCache, writeCache } = useFileCache(activeWorkspace?.id);

  // Y-1 Fix: handleCodeChange stale closure — activeFile.name her
  // useCallback yeniden oluşturulmadan önce eski değeri tutabilir.
  // Ref her zaman güncel değeri içerir.
  const activeFileNameCacheRef = useRef(activeFile.name);
  
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  // Yükleme kilidi: Tab değiştiğinde false, backend verisi gelince rAF ile true olur
  const isReadyToSaveRef = useRef(false);

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

  // Sayfa yüklendiğinde VEYA activeFile değiştiğinde içeriği getir
  useEffect(() => {
    const loadContent = async () => {
      if (!activeWorkspace?.id) return;

      // ─── ADIM 0: Kilit KAPAT — tab değişimi başladı ───
      // Monaco onChange, gerçek veri render edilmeden önce tetiklenirse
      // isReadyToSaveRef.current === false olduğu için kayıt engellenir.
      isReadyToSaveRef.current = false;

      // ─── ADIM 1: localStorage cache'den anında göster ───
      const cached = readCache(activeFile.name);
      if (cached !== null) {
        setCode(cached);
        // Cache'den gösterdik ama arka planda backend'den de tazeleyelim
        // (stale-while-revalidate pattern)
      } else {
        setCode('');
      }

      // ─── ADIM 2: Backend'den taze veri al ───
      try {
        let contentToSet = '';
        if (activeFile.name === 'main.py') {
          contentToSet = activeWorkspace.currentState || '';
        } else {
          const response = await api.get(`/workspace/${activeWorkspace.id}/file/${activeFile.name}/raw`);
          contentToSet = response.data.content || '';
        }

        setCode(contentToSet);
        // Cache'i taze veriyle güncelle
        writeCache(activeFile.name, contentToSet);
      } catch (err) {
        console.error('Dosya içeriği alınamadı:', err);
        if (cached === null) {
          // Cache de yok, hata mesajı göster
          setCode('// Dosya içeriği okunamadı veya yüklenemedi.');
        }
        // Cache varsa zaten gösterdik, hatayı sessizce geç
      } finally {
        // ─── ADIM SON: Kilit AÇ — bir sonraki render frame'ini bekle ───
        // requestAnimationFrame, Monaco'nun yeni değeri DOM'a basmasını
        // garantiler; setTimeout(0)'dan daha güvenilir.
        requestAnimationFrame(() => {
          isReadyToSaveRef.current = true;
        });
      }
    };

    if (mounted) {
      loadContent();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeWorkspace?.id, activeFile.name, mounted]);

  // Y-1 Fix: ref'i activeFile.name ile güncel tut
  useEffect(() => {
    activeFileNameCacheRef.current = activeFile.name;
  }, [activeFile.name]);

  const handleCodeChange = useCallback((newCode: string) => {
    setCode(newCode);
    // Y-1 Fix: activeFile.name yerine ref kullanılıyor — stale closure riski yok
    writeCache(activeFileNameCacheRef.current, newCode);
  }, [writeCache]); // activeFile.name bağımlılığı kalktı

  const handleRun = async () => {
    if (!activeWorkspace) return;
    
    setIsExecuting(true);
    setIsError(false);
    setOutputImages([]);       // Önceki grafikleri temizle
    setForceVisualTab(false);  // Sekme geçişini sıfırla
    setTerminalOutput('--- Yürütme Başladı ---\n\nYürütülüyor...');

    // Matplotlib & Styling Interceptor
    const interceptorCode = `
import matplotlib.pyplot as plt
import matplotlib
import os
try:
    plt.style.use('dark_background')
    matplotlib.rcParams['axes.prop_cycle'] = matplotlib.cycler(color=['#10b981', '#06b6d4', '#3b82f6', '#8b5cf6', '#f59e0b'])
    matplotlib.rcParams['font.family'] = 'sans-serif'
    matplotlib.rcParams['grid.alpha'] = 0.1
except:
    pass
` + "\n" + code;

    try {
      const response = await api.post(`/execution/${activeWorkspace.id}/run`, { code: interceptorCode });
      
      if (response.data && response.data.jobId) {
        const jobId = response.data.jobId;
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
                const outputText = data.output || data.Output || '';
                setTerminalOutput(outputText || 'Çalıştırma tamamlandı (Çıktı yok).');

                // Grafik tespiti: images dizisi dolu mu?
                const imgs = data.images || data.Images || [];
                if (imgs && imgs.length > 0) {
                  setOutputImages(imgs);
                  // TerminalOutput artık images değişimine direkt tepki veriyor,
                  // forceVisualTab ikili güvence olarak tutuluyor
                  setForceVisualTab(true);
                } else {
                  setOutputImages([]);
                  setForceVisualTab(false);
                }
              } else {
                setIsError(true);
                const errMsg = data.error || data.Error || 'Bilinmeyen hata';
                const errType = data.errorType || data.ErrorType || '';
                setTerminalOutput(errType ? `${errType}\n${errMsg}` : errMsg);
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
      
      <main className="flex-1 flex overflow-hidden relative z-0 after:absolute after:inset-x-0 after:top-0 after:h-4 after:bg-gradient-to-b after:from-black/[0.06] after:to-transparent after:pointer-events-none after:z-50">
        <ResizablePanels
          defaultLeftPercent={18}
          minLeftPercent={10}
          maxLeftPercent={35}
          leftPanel={<FileExplorer workspaceId={activeWorkspace.id} />}
          rightPanel={
            <ResizablePanels
              defaultLeftPercent={62}
              minLeftPercent={25}
              maxLeftPercent={80}
              leftPanel={
                activeFile.name.endsWith('.csv') ? (
                  <CsvTable csvData={code} fileName={activeFile.name} />
                ) : (
                  <CodeEditor workspaceId={activeWorkspace.id} code={code} setCode={handleCodeChange} isReadyToSaveRef={isReadyToSaveRef} />
                )
              }
              rightPanel={<TerminalOutput output={terminalOutput} isError={isError} images={outputImages} forceVisualTab={forceVisualTab} workspaceId={activeWorkspace.id} />}
            />
          }
        />
      </main>
      
      {/* Floating AI Interface */}
      <CurrereAI />
    </div>
  );
}
