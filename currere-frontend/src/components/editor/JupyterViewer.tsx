import { useState, useEffect } from 'react';
import { DiPython } from 'react-icons/di';

interface JupyterCell {
  cell_type: 'markdown' | 'code';
  source: string | string[];
}

interface JupyterNotebook {
  cells: JupyterCell[];
}

interface JupyterViewerProps {
  content: string;
}

export default function JupyterViewer({ content }: JupyterViewerProps) {
  const [notebook, setNotebook] = useState<JupyterNotebook | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => {
      try {
        if (!content.trim()) {
          setError('Not defteri içeriği boş.');
          return;
        }
        const parsed = JSON.parse(content);
        setNotebook(parsed);
        setError(null);
      } catch {
        setError('Notebook formatı geçersiz veya bozuk.');
      }
    }, 0);
    return () => clearTimeout(timer);
  }, [content]);

  const renderSource = (source: string | string[]) => {
    return Array.isArray(source) ? source.join('') : source;
  };

  if (error) {
    return (
      <div className="h-full flex items-center justify-center bg-[#1e1e1e] p-8">
        <div className="max-w-md w-full bg-red-500/10 border border-red-500/20 rounded-2xl p-6 text-center">
          <p className="text-red-400 text-sm font-medium mb-2">Hata Oluştu</p>
          <p className="text-zinc-400 text-xs leading-relaxed">{error}</p>
        </div>
      </div>
    );
  }

  if (!notebook) return null;

  return (
    <div className="h-full overflow-y-auto bg-[#1e1e1e] scroll-smooth p-6 md:p-10">
      <div className="max-w-4xl mx-auto space-y-8 pb-20">
        {notebook.cells.map((cell, idx) => (
          <div key={idx} className="group animate-in fade-in slide-in-from-bottom-4 duration-500 fill-mode-both" style={{ animationDelay: `${idx * 50}ms` }}>
            {cell.cell_type === 'markdown' ? (
              <div className="prose prose-invert max-w-none px-4">
                <p className="text-zinc-300 text-sm leading-relaxed whitespace-pre-wrap font-sans">
                  {renderSource(cell.source)}
                </p>
              </div>
            ) : (
              <div className="relative">
                <div className="absolute -left-10 top-2 hidden md:flex flex-col items-center opacity-20 group-hover:opacity-100 transition-opacity">
                  <span className="text-[10px] font-mono text-emerald-400 font-bold">[{idx}]</span>
                </div>
                <div className="bg-[#1a1a1a] border border-[#2d2d2d] group-hover:border-emerald-500/30 rounded-xl overflow-hidden shadow-lg transition-all duration-300">
                  <div className="bg-[#252525] px-4 py-1.5 border-b border-[#2d2d2d] flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <DiPython className="text-blue-400 w-4 h-4" />
                      <span className="text-[10px] font-bold text-zinc-500 tracking-widest uppercase">Python Hücresi</span>
                    </div>
                  </div>
                  <pre className="p-4 overflow-x-auto text-[13px] font-mono leading-relaxed text-emerald-300/90 bg-[#0d0d0d]/50">
                    <code>{renderSource(cell.source)}</code>
                  </pre>
                </div>
              </div>
            )}
          </div>
        ))}

        {notebook.cells.length === 0 && (
          <div className="text-center py-20 opacity-30 italic text-zinc-500 text-sm">
            Bu notebook henüz bir hücre içermiyor.
          </div>
        )}
      </div>
    </div>
  );
}
