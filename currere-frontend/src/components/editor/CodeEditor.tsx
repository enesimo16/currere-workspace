import Editor from '@monaco-editor/react';
import { useEffect, useRef } from 'react';
import api from '@/services/api';

interface CodeEditorProps {
  workspaceId?: string | number;
  code: string;
  setCode: (val: string) => void;
}

export default function CodeEditor({ workspaceId, code, setCode }: CodeEditorProps) {
  const isInitialMount = useRef(true);

  useEffect(() => {
    // İlk yüklemede kaydetme tetiklenmemesi için kontrol (Initial Mount)
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }

    if (!workspaceId) return;

    const timeoutId = setTimeout(async () => {
      try {
        await api.put(`/workspace/${workspaceId}/code`, { code });
        console.log('Kod başarıyla otomatik kaydedildi.');
      } catch (error) {
        console.error('Kod kaydedilirken hata oluştu:', error);
      }
    }, 1500);

    return () => clearTimeout(timeoutId);
  }, [code, workspaceId]);

  return (
    <section className="w-[60%] h-full border-r border-zinc-200 flex flex-col bg-[#1e1e1e]">
      {/* Subtle Editor Tab */}
      <div className="h-10 bg-[#2d2d2d] border-b border-[#1e1e1e] flex items-center px-4 shrink-0 shadow-sm">
        <div className="flex items-center gap-2 text-zinc-300 text-xs font-mono bg-[#1e1e1e] px-4 py-1.5 rounded-t-md border-t border-emerald-500/50">
          <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
          </svg>
          main.py
        </div>
      </div>
      
      <div className="flex-1 w-full relative">
        <Editor
          height="100%"
          defaultLanguage="python"
          theme="vs-dark"
          value={code}
          onChange={(val) => setCode(val || '')}
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
