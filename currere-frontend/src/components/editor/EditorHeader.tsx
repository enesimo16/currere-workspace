import { useState, useEffect } from 'react';
import { Workspace, useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { FiEdit2, FiTrash2, FiBox, FiCheck, FiX } from 'react-icons/fi';
import api from '@/services/api';

interface EditorHeaderProps {
  activeWorkspace: Workspace;
  isExecuting: boolean;
  onRun: () => void;
}

export default function EditorHeader({ activeWorkspace, isExecuting, onRun }: EditorHeaderProps) {
  const router = useRouter();
  const { setActiveWorkspace } = useWorkspaceStore();
  const [isRenaming, setIsRenaming] = useState(false);
  const [renameTitle, setRenameTitle] = useState(activeWorkspace.title || activeWorkspace.name || '');

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setRenameTitle(activeWorkspace.title || activeWorkspace.name || '');
  }, [activeWorkspace]);

  const handleRenameSubmit = async () => {
    if (!renameTitle.trim() || renameTitle === (activeWorkspace.title || activeWorkspace.name)) {
      setIsRenaming(false);
      return;
    }

    try {
      await api.put(`/workspace/${activeWorkspace.id}`, { title: renameTitle.trim() });
      setActiveWorkspace({ ...activeWorkspace, title: renameTitle.trim(), name: renameTitle.trim() });
      setIsRenaming(false);
    } catch (error) {
      console.error('Workspace rename error:', error);
      alert('Çalışma alanı adı güncellenemedi.');
    }
  };

  const handleDelete = async () => {
    if (!window.confirm('Bu çalışma alanını ve içindeki TÜM dosyaları silmek istediğinize emin misiniz?')) return;

    try {
      await api.delete(`/workspace/${activeWorkspace.id}`);
      router.push('/dashboard');
    } catch (error) {
      console.error('Workspace delete error:', error);
      alert('Çalışma alanı silinemedi.');
    }
  };

  return (
    <header className="h-14 border-b border-zinc-800 flex items-center justify-between px-4 shrink-0 bg-[#0a0a0a] shadow-lg z-10">
      <div className="flex items-center gap-4">
        <button
          onClick={() => router.push('/dashboard')}
          className="flex items-center gap-1.5 text-xs font-semibold text-zinc-400 hover:text-white hover:bg-zinc-800/50 px-3 py-1.5 rounded-lg transition-all tracking-wider"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          DASHBOARD
        </button>
        
        <div className="h-4 w-px bg-zinc-800"></div>
        
        <div className="flex items-center gap-3 group">
          <FiBox className="w-4 h-4 text-emerald-500" />
          
          {isRenaming ? (
            <div className="flex items-center gap-1 animate-in fade-in slide-in-from-left-2 duration-200">
              <input
                autoFocus
                type="text"
                value={renameTitle}
                onChange={(e) => setRenameTitle(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleRenameSubmit();
                  if (e.key === 'Escape') setIsRenaming(false);
                }}
                className="bg-zinc-900 border border-emerald-500/50 rounded px-2 py-0.5 text-sm text-zinc-100 outline-none w-48"
              />
              <button 
                onClick={handleRenameSubmit}
                className="p-1 text-emerald-400 hover:text-emerald-300 transition-colors"
                title="Kaydet"
              >
                <FiCheck className="w-4 h-4" />
              </button>
              <button 
                onClick={() => setIsRenaming(false)}
                className="p-1 text-zinc-500 hover:text-zinc-300 transition-colors"
                title="İptal"
              >
                <FiX className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-zinc-200 tracking-wide">
                {activeWorkspace.title || activeWorkspace.name || 'İsimsiz Çalışma Alanı'}
              </span>
              <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <button
                  onClick={() => setIsRenaming(true)}
                  className="p-1.5 text-zinc-500 hover:text-blue-400 transition-colors rounded-md hover:bg-zinc-800/30"
                  title="Yeniden Adlandır"
                >
                  <FiEdit2 className="w-3.5 h-3.5" />
                </button>
                <button
                  onClick={handleDelete}
                  className="p-1.5 text-zinc-500 hover:text-red-400 transition-colors rounded-md hover:bg-red-400/10"
                  title="Çalışma Alanını Sil"
                >
                  <FiTrash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center gap-4">
        <button
          onClick={onRun}
          disabled={isExecuting}
          className="flex items-center gap-2 bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-600/30 disabled:text-emerald-500/50 disabled:cursor-not-allowed text-white px-5 py-1.5 rounded-lg font-bold text-xs tracking-tighter transition-all shadow-[0_0_15px_rgba(16,185,129,0.2)] active:scale-95"
        >
          {isExecuting ? (
            <div className="w-4 h-4 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
          ) : (
            <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clipRule="evenodd" />
            </svg>
          )}
          {isExecuting ? 'EXECUTING...' : 'RUN CODE'}
        </button>
      </div>
    </header>
  );
}

