import Editor from '@monaco-editor/react';
import { useEffect, useRef, useState } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { FiAlertCircle, FiLoader } from 'react-icons/fi';
import toast from 'react-hot-toast';
import JupyterViewer from './JupyterViewer';

interface CodeEditorProps {
  workspaceId?: string | number;
  code: string;
  setCode: (val: string) => void;
}

export default function CodeEditor({ workspaceId, code, setCode }: CodeEditorProps) {
  const isInitialMount = useRef(true);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const editorRef = useRef<any>(null);
  const { activeFile, setActiveFile, pendingInjection, clearInjection } = useWorkspaceStore();
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [isConverting, setIsConverting] = useState(false);

  // Monaco Editor yüklendiğinde referansı kaydet
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleEditorDidMount = (editor: any) => {
    editorRef.current = editor;
  };

  // Kod Enjeksiyonu (AI Chat'ten gelen)
  useEffect(() => {
    if (pendingInjection && editorRef.current) {
      const editor = editorRef.current;
      const selection = editor.getSelection();
      const model = editor.getModel();

      if (model) {
        const range = selection || {
          startLineNumber: model.getLineCount(),
          startColumn: model.getLineMaxColumn(model.getLineCount()),
          endLineNumber: model.getLineCount(),
          endColumn: model.getLineMaxColumn(model.getLineCount())
        };

        const textToInsert = `\n${pendingInjection}\n`;

        editor.executeEdits('ai-injection', [
          {
            range: range,
            text: textToInsert,
            forceMoveMarkers: true
          }
        ]);

        clearInjection();
        editor.focus();
      }
    }
  }, [pendingInjection, clearInjection]);

  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = '';
      }
    };
    
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasUnsavedChanges]);

  useEffect(() => {
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }

    if (!workspaceId) return;

    const timeoutId = setTimeout(async () => {
      if (!activeFile?.name || code === undefined || code === null || !workspaceId) return;

      try {
        if (activeFile.name === 'main.py') {
          await api.put(`/workspace/${workspaceId}/code`, { code });
        } else {
          const blob = new Blob([code], { type: 'text/plain' });
          const formData = new FormData();
          formData.append('file', blob, activeFile.name);
          
          await api.put(`/workspace/${workspaceId}/file/${activeFile.name}`, formData, {
            headers: {
              'Content-Type': 'multipart/form-data'
            }
          });
        }
        
        setHasUnsavedChanges(false);
        console.log(`Kod başarıyla otomatik kaydedildi (${activeFile.name}).`);
      } catch (error: unknown) {
        if (axios.isAxiosError(error)) {
          const detail = error.response?.data || error.message || 'Bilinmeyen hata';
          console.error('Kayıt Hatası Detayı:', typeof detail === 'object' ? JSON.stringify(detail) : detail);
        }
      }
    }, 1000);

    return () => clearTimeout(timeoutId);
  }, [code, workspaceId, activeFile.name, activeFile]);

  const handleConvertNotebook = async () => {
    if (!activeFile.id || !workspaceId) {
      toast.error('Dosya ID bilgisi eksik. Lütfen dosyayı tekrar seçin.');
      return;
    }
    
    const toastId = toast.loading('Yapay zeka defteri Python script\'ine dönüştürüyor...');
    try {
      setIsConverting(true);
      const response = await api.post(`/workspace/${workspaceId}/ai/convert-ipynb-to-py`, {
        fileId: activeFile.id,
        prompt: "Clean and convert"
      });
      
      const convertedCode = response.data.code;
      const newFileName = activeFile.name.replace('.ipynb', '.py');
      
      // 1. Create file
      await api.post(`/workspace/${workspaceId}/file/create`, { fileName: newFileName });
      
      // 2. Update content
      const blob = new Blob([convertedCode], { type: 'text/plain' });
      const formData = new FormData();
      formData.append('file', blob, newFileName);
      await api.put(`/workspace/${workspaceId}/file/${newFileName}`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      
      toast.success('Dönüştürme işlemi başarılı!', { id: toastId });
      
      setActiveFile({
        id: response.data.newFileId || null, // Backend might return it, but for safety:
        name: newFileName,
        type: 'code'
      });
      
      window.location.reload();
      
    } catch {
      toast.error('Dosya dönüştürüleme hatası.', { id: toastId });
    } finally {
      setIsConverting(false);
    }
  };

  const isIpynb = activeFile.name.endsWith('.ipynb');
  const ext = activeFile.name.split('.').pop()?.toLowerCase();
  let defaultLang = 'python';
  if (ext === 'json') defaultLang = 'json';
  if (ext === 'csv' || ext === 'txt') defaultLang = 'plaintext';
  if (ext === 'sql') defaultLang = 'sql';

  return (
    <section className="h-full flex flex-col bg-[#1e1e1e] overflow-hidden">
      {/* Editor Header / Tab */}
      <div className="h-10 bg-[#2d2d2d] border-b border-[#1e1e1e] flex items-center justify-between px-4 shrink-0 shadow-sm">
        <div className="flex items-center gap-2 text-zinc-300 text-xs font-mono bg-[#1e1e1e] px-4 py-1.5 rounded-t-md border-t border-emerald-500/50">
          <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
          </svg>
          {activeFile.name}
        </div>

        {isIpynb && (
          <button
            onClick={handleConvertNotebook}
            disabled={isConverting}
            className={`flex items-center gap-2 px-3 py-1.5 rounded text-[10px] font-bold transition-all border ${
              isConverting 
                ? 'bg-emerald-900/30 text-emerald-500 border-emerald-500/30 cursor-wait opacity-70' 
                : 'bg-emerald-600/20 text-emerald-400 hover:bg-emerald-600/30 active:scale-95 border border-emerald-500/30'
            }`}
          >
            {isConverting ? (
              <>
                <FiLoader className="w-3 h-3 animate-spin" />
                <span className="tracking-widest capitalize">İŞLENİYOR...</span>
              </>
            ) : (
              <>
                <span className="text-sm leading-none">🪄</span> 
                <span className="tracking-widest">DÖNÜŞTÜR (.PY)</span>
              </>
            )}
          </button>
        )}
      </div>

      {/* IPYNB Warning Banner */}
      {isIpynb && (
        <div className="bg-emerald-500/5 border-b border-emerald-500/10 px-4 py-2 flex items-center gap-3">
          <FiAlertCircle className="text-emerald-500/60 w-4 h-4 shrink-0" />
          <p className="text-[11px] text-zinc-400 leading-relaxed tracking-wide">
            <span className="font-bold text-emerald-500/70">NOT:</span> Currere otonom motoru şu an Jupyter Notebook dosyalarını sadece okuyabilir, 
            ancak çalıştırmak için Python formatına dönüştürmeniz gereklidir.
          </p>
        </div>
      )}
      
      <div className="flex-1 w-full relative">
        {isIpynb ? (
          <JupyterViewer content={code} />
        ) : (
          <Editor
            height="100%"
            language={defaultLang}
            theme="vs-dark"
            value={code}
            onMount={handleEditorDidMount}
            onChange={(val) => {
              setCode(val || '');
              setHasUnsavedChanges(true);
            }}
            options={{
              minimap: { enabled: false },
              fontSize: 14,
              fontFamily: "'JetBrains Mono', 'Fira Code', Consolas, monospace",
              lineHeight: 1.6,
              padding: { top: 16 },
              scrollBeyondLastLine: false,
              smoothScrolling: true,
              cursorBlinking: "smooth",
              scrollbar: {
                verticalScrollbarSize: 8,
                horizontalScrollbarSize: 8,
              }
            }}
          />
        )}
      </div>
    </section>
  );
}
