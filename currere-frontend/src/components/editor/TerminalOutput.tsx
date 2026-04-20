interface TerminalOutputProps {
  output: string;
  isError: boolean;
}

export default function TerminalOutput({ output, isError }: TerminalOutputProps) {
  return (
    <section className="w-[40%] h-full bg-zinc-950 flex flex-col">
      {/* Terminal Header */}
      <div className="h-10 border-b border-zinc-800 flex items-center px-4 shrink-0 bg-zinc-900/50">
        <span className="text-xs font-medium text-zinc-400 uppercase tracking-widest flex items-center gap-2">
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          Terminal Çıktısı
        </span>
      </div>
      
      {/* Terminal Content Area */}
      <div className="flex-1 p-4 overflow-y-auto font-mono text-sm">
        <pre className={`whitespace-pre-wrap font-sans ${isError ? 'text-red-400' : 'text-zinc-300'}`}>
          {output}
        </pre>
      </div>
    </section>
  );
}
