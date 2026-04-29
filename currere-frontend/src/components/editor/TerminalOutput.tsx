import { useState, useEffect } from 'react';
import { FiTerminal, FiImage, FiActivity } from 'react-icons/fi';

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
    <section className="h-full bg-[#0d0d0d] flex flex-col">
      {/* Tab Header */}
      <div className="h-10 border-b border-zinc-800/70 flex items-center shrink-0 bg-[#141414] px-1 gap-0.5">
        <button 
          onClick={() => setActiveTab('terminal')}
          className={`h-8 px-4 flex items-center gap-2 text-[10px] font-bold tracking-widest rounded-md transition-all ${
            activeTab === 'terminal' 
              ? 'bg-zinc-800 text-emerald-400 shadow-inner' 
              : 'text-zinc-600 hover:text-zinc-400 hover:bg-zinc-800/40'
          }`}
        >
          <FiTerminal className="w-3.5 h-3.5" />
          TERMINAL
        </button>
        <button 
          onClick={() => setActiveTab('visual')}
          className={`h-8 px-4 flex items-center gap-2 text-[10px] font-bold tracking-widest rounded-md transition-all relative ${
            activeTab === 'visual' 
              ? 'bg-zinc-800 text-emerald-400 shadow-inner' 
              : 'text-zinc-600 hover:text-zinc-400 hover:bg-zinc-800/40'
          }`}
        >
          <FiImage className="w-3.5 h-3.5" />
          GÖRSEL ÇIKTI
          {images.length > 0 && (
            <span className="absolute -top-0.5 -right-0.5 w-3 h-3 bg-emerald-500 rounded-full flex items-center justify-center text-[7px] font-black text-black animate-pulse">
              {images.length}
            </span>
          )}
        </button>

        {/* Sağ: Durum */}
        <div className="ml-auto pr-3 flex items-center gap-2">
          {isError && (
            <span className="flex items-center gap-1 text-[9px] font-bold text-red-400 bg-red-500/10 px-2 py-0.5 rounded">
              <span className="w-1.5 h-1.5 rounded-full bg-red-500"></span>
              HATA
            </span>
          )}
          {!isError && output && !output.includes('Yürütülüyor') && (
            <span className="flex items-center gap-1 text-[9px] font-bold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
              TAMAMLANDI
            </span>
          )}
        </div>
      </div>
      
      {/* Content Area */}
      <div className="flex-1 overflow-y-auto relative">
        {activeTab === 'terminal' && selectedTerminalText && (
          <button 
            onClick={() => {
              addQuotedSnippet({ id: Date.now().toString(), type: 'terminal', content: selectedTerminalText });
              toast.success('Hata logu bağlama eklendi', {
                style: { background: '#333', color: '#fff', fontSize: '12px' }
              });
            }}
            className="absolute right-4 top-4 z-10 bg-red-600/90 hover:bg-red-500 text-white px-3 py-1.5 text-[10px] font-bold tracking-widest rounded shadow-xl backdrop-blur-md flex items-center gap-1.5 border border-red-400/30 transition-all active:scale-95"
          >
            🚨 AI'a Sor
          </button>
        )}
        {activeTab === 'terminal' ? (
          <div className="p-4 h-full">
            {outputLines.length > 0 ? (
              <div className="font-['JetBrains_Mono',monospace] text-[12px] space-y-0.5">
                {outputLines.map((line, idx) => {
                  const isErrLine = isError || line.includes('Error') || line.includes('Traceback') || line.includes('error:');
                  const isHeaderLine = line.startsWith('---');
                  return (
                    <div key={idx} className={`flex items-start gap-2 leading-relaxed px-2 rounded ${
                      isErrLine ? 'text-red-400 bg-red-950/20 py-0.5 border-l-2 border-red-500/50' : isHeaderLine ? 'text-zinc-500 italic' : 'text-zinc-300'
                    }`}>
                      <span className="text-zinc-700 text-[10px] select-none w-7 text-right shrink-0 pt-0.5 font-mono">
                        {idx + 1}
                      </span>
                      {isErrLine && <FiActivity className="w-3 h-3 text-red-500 shrink-0 mt-1" />}
                      <span className="whitespace-pre-wrap break-all">{line || ' '}</span>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="h-full flex items-center justify-center">
                <div className="text-center space-y-3 opacity-40">
                  <FiTerminal className="w-10 h-10 text-zinc-600 mx-auto" />
                  <p className="text-zinc-500 text-xs tracking-wide">Çalıştırmak için Çalıştır butonuna bas</p>
                </div>
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
                      alt="Otonom Grafik Çıktısı" 
                      className="w-full h-auto"
                      onError={(e) => (e.currentTarget.style.display = 'none')}
                      onLoad={(e) => (e.currentTarget.style.display = 'block')}
                    />
                    <div className="absolute inset-0 bg-emerald-500/5 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none"></div>
                   </div>
                   <p className="text-center text-[10px] text-emerald-500/60 mt-3 font-bold tracking-widest uppercase">Workspace: output_plot.png</p>
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
                  <FiImage className="w-10 h-10 text-zinc-700" />
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
