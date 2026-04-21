import Editor from '@monaco-editor/react';
import { useEffect, useRef, useState } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

interface CodeEditorProps {
  workspaceId?: string | number;
  code: string;
  setCode: (val: string) => void;
}

export default function CodeEditor({ workspaceId, code, setCode }: CodeEditorProps) {
  const isInitialMount = useRef(true);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const editorRef = useRef<any>(null);
  const { activeFile, pendingInjection, clearInjection } = useWorkspaceStore();
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

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

        // İşlem tamamlanınca state'i temizle
        clearInjection();
        
        // Görsel geri bildirim için odağı editöre ver
        editor.focus();
      }
    }
  }, [pendingInjection, clearInjection]);

  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = ''; // Modern browsers trigger the default dialog.
      }
    };
    
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasUnsavedChanges]);

  useEffect(() => {
    // İlk yüklemede kaydetme tetiklenmemesi için kontrol (Initial Mount)
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }

    if (!workspaceId) return;

    const timeoutId = setTimeout(async () => {
      // Ghost Save (Race Condition) Onarımı: Aktif dosya değişmiş veya silinmiş olabilir.
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
        } else if (error instanceof Error) {
          console.error('Kayıt Hatası Detayı:', error.message);
        } else {
          console.error('Kayıt Hatası Detayı:', String(error));
        }
      }
    }, 1000);

    return () => clearTimeout(timeoutId);
  }, [code, workspaceId, activeFile.name, activeFile]);

  const ext = activeFile.name.split('.').pop()?.toLowerCase();
  let defaultLang = 'python';
  if (ext === 'json') defaultLang = 'json';
  if (ext === 'csv' || ext === 'txt') defaultLang = 'plaintext';
  if (ext === 'sql') defaultLang = 'sql';

  return (
    <section className="h-full flex flex-col bg-[#1e1e1e] overflow-hidden">
      {/* Subtle Editor Tab */}
      <div className="h-10 bg-[#2d2d2d] border-b border-[#1e1e1e] flex items-center px-4 shrink-0 shadow-sm">
        <div className="flex items-center gap-2 text-zinc-300 text-xs font-mono bg-[#1e1e1e] px-4 py-1.5 rounded-t-md border-t border-emerald-500/50">
          <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
          </svg>
          {activeFile.name}
        </div>
      </div>
      
      <div className="flex-1 w-full relative">
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
      </div>
    </section>
  );
}
