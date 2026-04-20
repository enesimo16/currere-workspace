import { Workspace } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';

interface EditorHeaderProps {
  activeWorkspace: Workspace;
  isExecuting: boolean;
  onRun: () => void;
}

export default function EditorHeader({ activeWorkspace, isExecuting, onRun }: EditorHeaderProps) {
  const router = useRouter();

  return (
    <header className="h-14 border-b border-zinc-200 flex items-center justify-between px-4 shrink-0 bg-white shadow-sm z-10">
      <div className="flex items-center gap-4">
        <button
          onClick={() => router.push('/dashboard')}
          className="flex items-center gap-1.5 text-sm font-medium text-zinc-500 hover:text-zinc-900 hover:bg-zinc-100 px-3 py-1.5 rounded-lg transition-colors"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Dashboard
        </button>
        
        <div className="h-4 w-px bg-zinc-300"></div>
        
        <div className="flex items-center gap-2">
          <svg className="w-4 h-4 text-zinc-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <span className="text-sm font-medium text-zinc-800">
            {activeWorkspace.title || activeWorkspace.name || 'İsimsiz Çalışma Alanı'}
          </span>
        </div>
      </div>

      <button
        onClick={onRun}
        disabled={isExecuting}
        className="flex items-center gap-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-600/50 disabled:cursor-not-allowed text-white px-4 py-1.5 rounded-lg font-medium text-sm transition-all shadow-sm"
      >
        {isExecuting ? (
          <div className="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin"></div>
        ) : (
          <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clipRule="evenodd" />
          </svg>
        )}
        {isExecuting ? 'Running...' : 'Run'}
      </button>
    </header>
  );
}
