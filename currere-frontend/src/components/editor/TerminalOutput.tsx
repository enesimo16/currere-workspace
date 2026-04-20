import { useState } from 'react';
import { FiTerminal, FiImage } from 'react-icons/fi';

interface TerminalOutputProps {
  output: string;
  isError: boolean;
  images?: string[]; // Base64 image strings
}

export default function TerminalOutput({ output, isError, images = [] }: TerminalOutputProps) {
  const [activeTab, setActiveTab] = useState<'terminal' | 'visual'>('terminal');

  return (
    <section className="w-[40%] h-full bg-zinc-950 flex flex-col border-l border-zinc-800">
      {/* Tab Header */}
      <div className="h-10 border-b border-zinc-800 flex items-center shrink-0 bg-zinc-900/50">
        <button 
          onClick={() => setActiveTab('terminal')}
          className={`h-full px-4 flex items-center gap-2 text-[10px] font-bold tracking-widest transition-colors ${
            activeTab === 'terminal' ? 'bg-zinc-800 text-emerald-400 border-b-2 border-emerald-500' : 'text-zinc-500 hover:text-zinc-300'
          }`}
        >
          <FiTerminal className="w-3.5 h-3.5" />
          TERMINAL
        </button>
        <button 
          onClick={() => setActiveTab('visual')}
          className={`h-full px-4 flex items-center gap-2 text-[10px] font-bold tracking-widest transition-colors ${
            activeTab === 'visual' ? 'bg-zinc-800 text-emerald-400 border-b-2 border-emerald-500' : 'text-zinc-500 hover:text-zinc-300'
          }`}
        >
          <FiImage className="w-3.5 h-3.5" />
          GÖRSEL ÇIKTI
        </button>
      </div>
      
      {/* Content Area */}
      <div className="flex-1 overflow-y-auto font-mono text-sm relative">
        {activeTab === 'terminal' ? (
          <div className="p-4">
            <pre className={`whitespace-pre-wrap font-sans ${isError ? 'text-red-400' : 'text-zinc-300'}`}>
              {output || 'Çıktı bekleniyor...'}
            </pre>
          </div>
        ) : (
          <div className="p-6 h-full flex flex-col items-center justify-center">
            {images && images.length > 0 ? (
              <div className="w-full space-y-6">
                {images.map((img, idx) => (
                  <div key={idx} className="bg-white rounded-xl overflow-hidden shadow-2xl p-2 border border-zinc-800">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img src={`data:image/png;base64,${img}`} alt={`Çıktı ${idx + 1}`} className="w-full h-auto" />
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center space-y-4 max-w-[280px]">
                <div className="w-16 h-16 bg-zinc-900 rounded-2xl flex items-center justify-center mx-auto border border-zinc-800 shadow-inner">
                  <FiImage className="w-8 h-8 text-zinc-700" />
                </div>
                <div className="space-y-1">
                  <p className="text-zinc-300 font-semibold text-xs tracking-wide">Grafik Bekleniyor</p>
                  <p className="text-zinc-500 text-[10px] leading-relaxed">
                    Python kodunuzda Matplotlib veya Seaborn ile grafik çizdirdiğinizde burada görünecektir.
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
