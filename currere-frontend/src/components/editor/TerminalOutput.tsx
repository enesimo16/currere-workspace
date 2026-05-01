import { useState, useEffect } from 'react';
import { FiStar, FiActivity } from 'react-icons/fi';

interface TerminalOutputProps {
  output: string;
  isError: boolean;
  images?: string[];
  forceVisualTab?: boolean;
}

import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import toast from 'react-hot-toast';

export default function TerminalOutput({ output, isError, images = [], forceVisualTab = false, workspaceId }: TerminalOutputProps & { workspaceId?: string | number }) {
  const [activeTab, setActiveTab] = useState<'terminal' | 'visual'>('terminal');
  const [plotTimestamp, setPlotTimestamp] = useState(Date.now());
  const [selectedTerminalText, setSelectedTerminalText] = useState('');
  const { addQuotedSnippet } = useWorkspaceStore();

  useEffect(() => {
    const handleSelection = () => {
      if (activeTab !== 'terminal') return;
      const text = window.getSelection()?.toString() || '';
      setSelectedTerminalText(text);
    };
    document.addEventListener('selectionchange', handleSelection);
    return () => document.removeEventListener('selectionchange', handleSelection);
  }, [activeTab]);

  // Workspace'teki statik grafiği göstermek için URL
  const plotUrl = `/api/workspace/${workspaceId}/file/output_plot.png?t=${plotTimestamp}`;

  // Yürütme bittiğinde grafiği tazele
  useEffect(() => {
    if (!output.includes('Yürütülüyor') && output !== '') {
      setPlotTimestamp(Date.now());
    }
  }, [output]);

  // Görsel çıktı varsa otomatik sekme geçişi
  useEffect(() => {
    if (forceVisualTab && images.length > 0) {
      const t = setTimeout(() => setActiveTab('visual'), 0);
      return () => clearTimeout(t);
    }
  }, [forceVisualTab, images]);

  const outputLines = output ? output.split('\n') : [];

  return (
    <section className="h-full bg-zinc-950/80 backdrop-blur-sm flex flex-col border-l border-zinc-800/50">
      {/* Tab Header */}
      <div className="h-10 border-b border-zinc-900/50 flex items-end px-6 shrink-0 bg-transparent gap-8">
        <button 
          onClick={() => setActiveTab('terminal')}
          className={`pb-1.5 text-[10px] font-mono tracking-[0.2em] uppercase transition-all border-b-2 ${
            activeTab === 'terminal' 
              ? 'text-zinc-200 border-zinc-400' 
              : 'text-zinc-500 hover:text-zinc-300 border-transparent'
          }`}
        >
          TERMINAL
        </button>
        <button 
          onClick={() => setActiveTab('visual')}
          className={`pb-1.5 text-[10px] font-mono tracking-[0.2em] uppercase transition-all border-b-2 relative ${
            activeTab === 'visual' 
              ? 'text-zinc-200 border-zinc-400' 
              : 'text-zinc-500 hover:text-zinc-300 border-transparent'
          }`}
        >
          GÖRSEL ÇIKTI
          {images.length > 0 && (
            <span className="absolute -top-1 -right-4 w-2.5 h-2.5 bg-zinc-500/20 border border-zinc-500/40 rounded-full flex items-center justify-center text-[6px] font-bold text-zinc-400 animate-pulse">
            </span>
          )}
        </button>

        {/* Sağ: Durum */}
        <div className="ml-auto pb-1.5 pr-2 flex items-center gap-3">
          {isError && (
            <span className="flex items-center gap-1.5 text-[8px] font-medium tracking-widest text-red-400/80 uppercase">
              <span className="w-1 h-1 rounded-full bg-red-500/50 shadow-[0_0_8px_rgba(239,68,68,0.5)]"></span>
              ERROR
            </span>
          )}
          {!isError && output && !output.includes('Yürütülüyor') && (
            <span className="flex items-center gap-1.5 text-[8px] font-medium tracking-widest text-zinc-400/80 uppercase">
              <span className="w-1 h-1 rounded-full bg-zinc-500/50 shadow-[0_0_8px_rgba(161,161,170,0.5)]"></span>
              SYNCED
            </span>
          )}
        </div>
      </div>
      
      {/* Content Area */}
      <div className="flex-1 overflow-y-auto relative custom-scrollbar">
        {activeTab === 'terminal' && selectedTerminalText && (
          <button 
            onClick={() => {
              addQuotedSnippet({ id: Date.now().toString(), type: 'terminal', content: selectedTerminalText });
              toast.success('Analiz bağlamı eklendi', {
                style: { background: '#09090b', color: '#a1a1aa', fontSize: '11px', border: '1px solid #27272a' }
              });
            }}
            className="absolute right-6 top-6 z-10 bg-zinc-900/90 hover:bg-zinc-800 text-zinc-300 px-4 py-2 text-[10px] font-medium tracking-[0.1em] rounded-full shadow-2xl backdrop-blur-xl flex items-center gap-2 border border-zinc-800 transition-all active:scale-95"
          >
            AI'A AKTAR
          </button>
        )}
        {activeTab === 'terminal' ? (
          <div className="p-6 h-full">
            {outputLines.length > 0 ? (
              <div className="font-mono text-[12px] space-y-1">
                {outputLines.map((line, idx) => {
                  const isErrLine = isError || line.includes('Error') || line.includes('Traceback') || line.includes('error:');
                  const isHeaderLine = line.startsWith('---');
                  return (
                    <div key={idx} className={`flex items-start gap-3 leading-relaxed px-3 rounded-sm transition-colors ${
                      isErrLine ? 'text-red-400/90 bg-red-500/5 border-l border-red-500/30' : isHeaderLine ? 'text-zinc-600 italic' : 'text-zinc-400'
                    } hover:bg-zinc-800/20`}>
                      <span className="text-zinc-800 text-[9px] select-none w-8 text-right shrink-0 pt-1 font-mono tracking-tighter opacity-50">
                        {idx + 1}
                      </span>
                      <span className="whitespace-pre-wrap break-all">{line || ' '}</span>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="h-full flex items-center justify-center">
                <p className="text-zinc-600 text-xs font-light tracking-wide italic">Komut çıktısı burada gözükecektir...</p>
              </div>
            )}
          </div>
        ) : (
          <div className="p-6 h-full flex flex-col items-center justify-center w-full overflow-y-auto">
            {images.length > 0 || workspaceId ? (
              <div className="w-full space-y-6 py-4 max-w-2xl">
                {/* 1. Statik output_plot.png (Her zaman dene) */}
                <div className="bg-[#141414] rounded-2xl overflow-hidden shadow-2xl border border-zinc-800 p-2 group transition-all">
                   <div className="relative overflow-hidden rounded-xl">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img 
                      src={plotUrl} 
                      alt="Grafik Çıktısı" 
                      className="w-full h-auto"
                      onError={(e) => (e.currentTarget.style.display = 'none')}
                      onLoad={(e) => (e.currentTarget.style.display = 'block')}
                    />
                    <div className="absolute inset-0 bg-zinc-500/5 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none"></div>
                   </div>
                   <p className="text-center text-[10px] text-zinc-500/80 mt-3 font-bold tracking-widest uppercase">Workspace: output_plot.png</p>
                </div>

                {/* 2. Base64 Gelenler */}
                {images.map((img, idx) => (
                  <div key={idx} className="bg-[#141414] rounded-2xl overflow-hidden shadow-2xl border border-zinc-800 p-2 transition-all">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img 
                      src={`data:image/png;base64,${img}`} 
                      alt={`Görsel Çıktı ${idx + 1}`} 
                      className="w-full h-auto rounded-xl" 
                    />
                    <p className="text-center text-[10px] text-zinc-500 mt-2 font-mono">Hafıza Grafik #{idx + 1}</p>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center space-y-4 max-w-[260px] opacity-40">
                <div className="w-20 h-20 bg-zinc-900 rounded-3xl flex items-center justify-center mx-auto border border-zinc-800 shadow-inner">
                  <FiAperture className="w-10 h-10 text-zinc-700" />
                </div>
                <div className="space-y-1.5">
                  <p className="text-zinc-400 font-semibold text-xs tracking-wide">Görsel Çıktı Bekleniyor</p>
                  <p className="text-zinc-600 text-[11px] leading-relaxed">
                    Matplotlib veya Seaborn ile grafik oluşturduğunuzda burada görünecektir.
                  </p>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
