import Editor from '@monaco-editor/react';
import { useEffect, useRef, useState } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { FiLoader, FiSave, FiCode, FiDownload, FiX, FiRefreshCw } from 'react-icons/fi';
import toast from 'react-hot-toast';
import JupyterViewer from './JupyterViewer';
import { useSync } from '@/hooks/useSync';

interface CodeEditorProps {
  workspaceId?: string | number;
  code: string;
  setCode: (val: string) => void;
  isReadyToSaveRef: { current: boolean }; // Parent'tan gelen kilit — tab geçişinde false, veri hazır olunca true
}

export default function CodeEditor({ workspaceId, code, setCode, isReadyToSaveRef }: CodeEditorProps) {
  const saveTimerRef = useRef<NodeJS.Timeout | null>(null);
  // AI Completion ref'leri — component scope'ta yaşar, unmount'ta tam temizlenir (Bug #3)
  const completionDebounceRef = useRef<NodeJS.Timeout | null>(null);
  const completionAbortRef = useRef<AbortController | null>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const editorRef = useRef<any>(null);
  const currentCodeRef = useRef(code);
  const providerRef = useRef<any>(null);
  const { activeFile, setActiveFile, openFiles, removeOpenFile, pendingInjection, clearInjection, addQuotedSnippet } = useWorkspaceStore();
  const activeFileNameRef = useRef(activeFile?.name);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [selectedCode, setSelectedCode] = useState('');
  const [isConverting, setIsConverting] = useState(false);
  
  // VS Code Sync
  const { sendUpdate, isRemoteUpdateRef } = useSync(workspaceId);

  // Monaco Editor yüklendiğinde referansı kaydet
  const handleEditorDidMount = (editor: any, monaco: any) => {
    editorRef.current = editor;
    
    if (providerRef.current) {
      providerRef.current.dispose();
    }

    // ── GHOST TEXT (INLINE COMPLETIONS) ENTEGRASYONU ── (Bug #2 + #3 Fix)
    providerRef.current = monaco.languages.registerInlineCompletionsProvider('*', {
      provideInlineCompletions: (model: any, position: any, context: any, token: any) => {
        // Önceki timer ve uçuştaki isteği anında temizle
        if (completionDebounceRef.current) clearTimeout(completionDebounceRef.current);
        if (completionAbortRef.current) completionAbortRef.current.abort();

        return new Promise((resolve) => {
          // KRİTİK: Monaco bu completion'ı iptal ederse Promise anında kapanır.
          // Böylece kuyruğun birikmesi ve 429 spam önlenir. (Bug #2)
          token.onCancellationRequested(() => resolve({ items: [] }));

          completionDebounceRef.current = setTimeout(async () => {
            if (token.isCancellationRequested || !workspaceId) {
              return resolve({ items: [] });
            }

            completionAbortRef.current = new AbortController();

            try {
              const codeValue = model.getValue();
              if (!codeValue || codeValue.trim() === '') return resolve({ items: [] });

              const response = await api.post(`/workspace/${workspaceId}/ai/inline-complete`, {
                code: codeValue,
                cursorLine: position.lineNumber,
                cursorCol: position.column
              }, {
                signal: completionAbortRef.current.signal
              });

              const completionText = response.data?.completion;
              if (completionText) {
                resolve({
                  items: [{
                    insertText: completionText,
                    range: new monaco.Range(position.lineNumber, position.column, position.lineNumber, position.column)
                  }]
                });
              } else {
                resolve({ items: [] });
              }
            } catch {
              // Hata veya Abort durumunda sessizce boş dön
              resolve({ items: [] });
            }
          }, 750);
        });
      },
      freeInlineCompletions: () => {}
    });
    // ────────────────────────────────────────────────

    editor.onDidChangeCursorSelection(() => {
      const selection = editor.getSelection();
      const model = editor.getModel();
      if (model && selection && !selection.isEmpty()) {
        setSelectedCode(model.getValueInRange(selection));
      } else {
        setSelectedCode('');
      }
    });
  };

  // Cleanup: Monaco Provider + AI Completion zamanlayıcı ve isteği (Bug #3 Fix)
  useEffect(() => {
    return () => {
      // 1. Bekleyen debounce timer'ı temizle
      if (completionDebounceRef.current) clearTimeout(completionDebounceRef.current);
      // 2. Havada uçan API isteğini iptal et
      if (completionAbortRef.current) completionAbortRef.current.abort();
      // 3. Monaco provider'ı dispose et
      if (providerRef.current) providerRef.current.dispose();
    };
  }, []);

  // Aktif dosya ismi referansı (Closure fix)
  useEffect(() => {
    activeFileNameRef.current = activeFile?.name;
  }, [activeFile?.name]);

  // NOT: isReadyToSave useEffect'leri KALDIRILDI (Bug #1 Fix)
  // Kilit yönetimi artık parent editor/page.tsx içinde isReadyToSaveRef ile yapılıyor.

  // Sync Update Listener
  useEffect(() => {
    const handleSyncUpdate = (e: any) => {
      if (e.detail !== undefined) {
        setCode(e.detail);
      }
    };
    window.addEventListener('editor-sync-update', handleSyncUpdate);
    return () => window.removeEventListener('editor-sync-update', handleSyncUpdate);
  }, [setCode]);

  // Kod Enjeksiyonu (AI Chat'ten gelen)
  useEffect(() => {
    if (pendingInjection && editorRef.current) {
      const editor = editorRef.current;
      const position = editor.getPosition();
      const selection = editor.getSelection();
      const model = editor.getModel();
      
      if (model && position && selection) {
        const hasSelection = !selection.isEmpty();
        const range = hasSelection ? selection : {
          startLineNumber: position.lineNumber,
          startColumn: position.column,
          endLineNumber: position.lineNumber,
          endColumn: position.column
        };
        
        const textToInsert = hasSelection ? pendingInjection : `\n${pendingInjection}\n`;
        editor.executeEdits('ai-injection', [{ range, text: textToInsert, forceMoveMarkers: true }]);
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
      
      // O-3 Fix: window.location.reload() yerine state reset kullanılıyor.
      // setActiveFile → parent'ın useEffect [activeFile.name] tetiklenir → içerik yeniden yüklenir.
      setActiveFile({
        id: response.data.newFileId || null,
        name: newFileName,
        type: 'code'
      });
      
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
      {/* Editor Header / Tab Bar */}
      <div className="h-10 bg-[#0c0c0e] border-b border-zinc-800/50 flex items-end justify-between shrink-0 relative z-10">
        <div className="flex items-end pl-2 overflow-x-auto overflow-y-hidden no-scrollbar scrollbar-hide max-w-[calc(100%-150px)]">
          {openFiles.map((file) => {
            const isActive = activeFile.name === file.name;
            return (
              <div 
                key={file.name}
                onClick={() => setActiveFile(file)}
                className={`
                  group flex items-center gap-2 text-xs font-mono px-4 py-2 cursor-pointer transition-all relative -bottom-[1px]
                  ${isActive 
                    ? 'bg-[#1e1e1e] text-zinc-200 border-t-2 border-t-zinc-400/50 border-x border-x-zinc-800/50 border-b border-b-[#1e1e1e] z-20' 
                    : 'bg-transparent text-zinc-500 hover:bg-zinc-800/30 hover:text-zinc-300 border-b border-b-transparent'
                  }
                `}
              >
                <svg className={`w-3.5 h-3.5 ${isActive ? 'text-zinc-300' : 'text-zinc-500 group-hover:text-zinc-400'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                </svg>
                <span className="truncate max-w-[120px]">{file.name}</span>
                
                {/* Close Button - Hide for main.py if it's the only one, or always allow if logic supports it */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    removeOpenFile(file.name);
                  }}
                  className={`
                    p-0.5 rounded-sm transition-colors
                    ${isActive ? 'hover:bg-zinc-800 text-zinc-400' : 'opacity-0 group-hover:opacity-100 hover:bg-zinc-800 text-zinc-500'}
                  `}
                >
                  <FiX className="w-3 h-3" />
                </button>
              </div>
            );
          })}
        </div>

        <div className="pb-1.5 pr-4 flex items-center gap-4">
          {isIpynb && (
            <button
              onClick={handleConvertNotebook}
              disabled={isConverting}
              className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-white text-zinc-900 hover:bg-zinc-100 hover:scale-[1.02] active:scale-[0.98] transition-all shadow-md hover:shadow-lg font-bold text-xs disabled:opacity-50 disabled:cursor-wait"
            >
              {isConverting ? (
                <>
                  <FiLoader className="w-4 h-4 animate-spin" />
                  <span className="tracking-wide">İŞLENİYOR...</span>
                </>
              ) : (
                <>
                  <FiRefreshCw className="w-4 h-4" />
                  <span className="tracking-wide">DÖNÜŞTÜR (.PY)</span>
                </>
              )}
            </button>
          )}
        </div>
      </div>
      
      <div className="flex-1 w-full relative">
        {!isIpynb && selectedCode && (
          <button 
            onClick={() => {
              addQuotedSnippet({ id: Date.now().toString(), type: 'code', content: selectedCode });
              toast.success('Seçili kod bağlama eklendi', {
                style: { background: '#333', color: '#fff', fontSize: '12px' }
              });
            }}
            className="absolute right-8 top-4 z-10 bg-zinc-800/90 hover:bg-zinc-700 text-zinc-100 px-3 py-1.5 text-[10px] font-bold tracking-widest rounded shadow-xl backdrop-blur-md flex items-center gap-1.5 border border-zinc-700/30 transition-all active:scale-95"
          >
            AI'A GÖNDER
          </button>
        )}
        {isIpynb ? (
          <JupyterViewer content={code} workspaceId={workspaceId || ''} activeFileName={activeFile.name} />
        ) : (
          <Editor
            height="100%"
            language={defaultLang}
            theme="vs-dark"
            value={code}
            onMount={handleEditorDidMount}
            onChange={(val) => {
              // Kilit kontrolü — parent'tan gelen isReadyToSaveRef (Bug #1 Fix)
              if (!isReadyToSaveRef.current) return;

              const currentVal = val || '';
              setCode(currentVal);
              currentCodeRef.current = currentVal;
              setHasUnsavedChanges(true);

              const fileToSave = activeFileNameRef.current;

              if (!workspaceId || !fileToSave || currentCodeRef.current.trim() === '') return;
              if (fileToSave.endsWith('.ipynb')) return;

              if (saveTimerRef.current) clearTimeout(saveTimerRef.current);

              saveTimerRef.current = setTimeout(async () => {
                const codeToSave = currentCodeRef.current;
                const finalFileToSave = activeFileNameRef.current; // State'ten değil, ref'ten oku!

                // Çift güvence: timer ateşlendiğinde kilit hâlâ kapalıysa kaydetme
                if (!finalFileToSave || !isReadyToSaveRef.current) return;

                try {
                  if (finalFileToSave === 'main.py') {
                    await api.put(`/workspace/${workspaceId}/code`, { code: codeToSave });
                  } else {
                    const blob = new Blob([codeToSave], { type: 'text/plain' });
                    const formData = new FormData();
                    formData.append('file', blob, finalFileToSave);
                    await api.put(`/workspace/${workspaceId}/file/${finalFileToSave}`, formData);
                  }
                  setHasUnsavedChanges(false);
                  // O-2 Fix: Remote update ise (SignalR'dan gelen) sendUpdate'i ATLA.
                  // isRemoteUpdateRef.current, useSync'te ReceiveCodeUpdate sırasında true yapılır.
                  // Bu kontrol Ping-Pong (echo loop) döngüsünü kırar.
                  if (!isRemoteUpdateRef.current) {
                    sendUpdate(finalFileToSave, codeToSave);
                  }
                  isRemoteUpdateRef.current = false; // Her durumda sıfırla
                } catch (error) {
                  console.error('Kayıt Hatası:', error);
                }
              }, 300);
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
              // backgroundColor kaldırıldı — Monaco IStandaloneEditorConstructionOptions'da yok
              // Arkaplan rengi theme="vs-dark" ile kontrol edilir
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
