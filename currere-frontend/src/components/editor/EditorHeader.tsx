import { useState, useEffect } from 'react';
import { Workspace, useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { FiEdit2, FiTrash2, FiBox, FiCheck, FiX, FiMonitor, FiCopy, FiTerminal, FiExternalLink } from 'react-icons/fi';
import api from '@/services/api';
import toast from 'react-hot-toast';
import { useAuthStore } from '@/store/useAuthStore';
import { SiHuggingface } from 'react-icons/si';

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
  const [isSyncModalOpen, setIsSyncModalOpen] = useState(false);
  const [syncToken, setSyncToken] = useState('');
  const { huggingFaceToken } = useAuthStore();
  const { activeFile } = useWorkspaceStore();

  // Push to Hub Modal States
  const [isPushModalOpen, setIsPushModalOpen] = useState(false);
  const [repoName, setRepoName] = useState(activeWorkspace.title?.toLowerCase().replace(/\s+/g, '-') || '');
  const [commitMsg, setCommitMsg] = useState('Otonom Currere AI tarafından aktarıldı');
  const [isPrivate, setIsPrivate] = useState(false);
  const [isPushing, setIsPushing] = useState(false);

  useEffect(() => {
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
      toast.success('Çalışma alanı adı güncellendi.');
    } catch (error) {
      console.error('Workspace rename error:', error);
      toast.error('Ad güncellenemedi.');
    }
  };

  const handleDelete = async () => {
    if (!window.confirm('Bu çalışma alanını ve içindeki TÜM dosyaları silmek istediğinize emin misiniz?')) return;

    try {
      await api.delete(`/workspace/${activeWorkspace.id}`);
      router.push('/dashboard');
    } catch (error) {
      console.error('Workspace delete error:', error);
      toast.error('Çalışma alanı silinemedi.');
    }
  };

  const openSyncModal = async () => {
    const toastId = toast.loading('Bağlantı anahtarı üretiliyor...');
    try {
      const response = await api.get(`/workspace/${activeWorkspace.id}/sync-token`);
      if (response.data?.token) {
        setSyncToken(response.data.token);
        setIsSyncModalOpen(true);
        toast.dismiss(toastId);
      }
    } catch (error) {
      console.error('Sync token error:', error);
      toast.error('Bağlantı anahtarı alınamadı.', { id: toastId });
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success('Komut kopyalandı!');
  };

  const handlePushToHub = async () => {
    if (!huggingFaceToken) {
      toast.error('Önce ayarlar kısmından Hugging Face Token giriniz.');
      return;
    }

    if (!repoName.trim()) {
      toast.error('Lütfen bir repo adı giriniz.');
      return;
    }

    const toastId = toast.loading('Hugging Face Hub\'a aktarılıyor...');
    setIsPushing(true);

    try {
      const response = await api.post(`/workspace/${activeWorkspace.id}/ai/push-to-huggingface`, {
        repoName: repoName.trim(),
        isPrivate: isPrivate,
        commitMessage: commitMsg.trim(),
        fileName: activeFile.name,
        hfToken: huggingFaceToken
      });

      toast.success(
        (t) => (
          <span className="flex items-center gap-2">
            Başarıyla aktarıldı! 
            <a href={response.data.url} target="_blank" rel="noreferrer" className="text-emerald-500 font-bold underline" onClick={() => toast.dismiss(t.id)}>
              Hub'da Görüntüle
            </a>
          </span>
        ),
        { id: toastId, duration: 6000 }
      );
      setIsPushModalOpen(false);
    } catch (err: any) {
      const errMsg = err.response?.data?.error || 'Aktarım başarısız oldu.';
      toast.error(errMsg, { id: toastId });
    } finally {
      setIsPushing(false);
    }
  };

  return (
    <header className="h-14 border-b border-zinc-800/50 flex items-center justify-between px-6 shrink-0 bg-[#0a0a0a]/80 backdrop-blur-md shadow-2xl z-40 relative">
      <div className="flex items-center gap-4">
        <button
          onClick={() => router.push('/dashboard')}
          className="group flex items-center gap-2 text-[10px] font-black text-zinc-500 hover:text-white transition-all tracking-[0.2em] outline-none"
        >
          <svg className="w-3.5 h-3.5 transform group-hover:-translate-x-1 transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M15 19l-7-7 7-7" />
          </svg>
          DASHBOARD
        </button>
        
        <div className="h-6 w-px bg-zinc-800/80"></div>
        
        <div className="flex items-center gap-3 group">
          <div className="p-1.5 bg-emerald-500/10 rounded-md">
            <FiBox className="w-3.5 h-3.5 text-emerald-500" />
          </div>
          
          {isRenaming ? (
            <div className="flex items-center gap-1.5 animate-in fade-in slide-in-from-left-2 duration-300">
              <input
                autoFocus
                type="text"
                value={renameTitle}
                onChange={(e) => setRenameTitle(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleRenameSubmit();
                  if (e.key === 'Escape') setIsRenaming(false);
                }}
                className="bg-zinc-900/50 border border-emerald-500/30 rounded-md px-3 py-1 text-xs text-zinc-100 outline-none w-56 focus:border-emerald-500/60 transition-all font-mono"
              />
              <button onClick={handleRenameSubmit} className="p-1.5 text-emerald-400 hover:bg-emerald-400/10 rounded-md transition-all"><FiCheck className="w-3.5 h-3.5" /></button>
              <button onClick={() => setIsRenaming(false)} className="p-1.5 text-zinc-500 hover:bg-zinc-500/10 rounded-md transition-all"><FiX className="w-3.5 h-3.5" /></button>
            </div>
          ) : (
            <div className="flex items-center gap-3">
              <span className="text-sm font-bold text-zinc-100 tracking-tight flex items-center gap-2">
                {activeWorkspace.title || activeWorkspace.name || 'İsimsiz Proje'}
                <span className="text-[9px] px-1.5 py-0.5 bg-zinc-800 text-zinc-500 rounded uppercase tracking-widest font-black border border-zinc-700/50">SANDBOX</span>
              </span>
              <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-all">
                <button onClick={() => setIsRenaming(true)} className="p-1.5 text-zinc-500 hover:text-blue-400 transition-colors rounded-md hover:bg-zinc-800/50"><FiEdit2 className="w-3 h-3" /></button>
                <button onClick={handleDelete} className="p-1.5 text-zinc-500 hover:text-red-400 transition-colors rounded-md hover:bg-red-400/5"><FiTrash2 className="w-3 h-3" /></button>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center gap-3">
        {/* VS Code Connect Button */}
        <button
          onClick={openSyncModal}
          className="flex items-center gap-2 px-4 py-1.5 rounded-lg text-[10px] font-bold text-zinc-400 hover:text-emerald-400 hover:bg-emerald-400/5 border border-zinc-800 hover:border-emerald-500/30 transition-all tracking-wider"
        >
          <FiMonitor className="w-3.5 h-3.5" />
          VS CODE'A BAĞLAN
        </button>

        <button
          onClick={() => setIsPushModalOpen(true)}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-[10px] font-bold text-zinc-400 hover:text-amber-400 hover:bg-amber-400/5 border border-zinc-800 hover:border-amber-500/30 transition-all tracking-wider"
        >
          <SiHuggingface className="w-3.5 h-3.5" />
          PUSH TO HUB
        </button>

        <div className="w-px h-6 bg-zinc-800/80 mx-1"></div>

        <button
          onClick={onRun}
          disabled={isExecuting}
          className="relative flex items-center gap-2 bg-emerald-600 hover:bg-emerald-500 disabled:bg-zinc-800 disabled:text-zinc-600 disabled:border-zinc-700 border border-emerald-500/50 text-white px-6 py-1.5 rounded-lg font-black text-[11px] tracking-[0.1em] transition-all shadow-xl active:scale-95 disabled:cursor-not-allowed group overflow-hidden"
        >
          <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/10 to-transparent -translate-x-full group-hover:translate-x-full transition-transform duration-700"></div>
          {isExecuting ? (
            <div className="w-3.5 h-3.5 border-[3px] border-white/20 border-t-white rounded-full animate-spin"></div>
          ) : (
            <svg className="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clipRule="evenodd" />
            </svg>
          )}
          {isExecuting ? 'YÜRÜTÜLÜYOR...' : 'KODU ÇALIŞTIR'}
        </button>
      </div>

      {/* Sync Modal Overlay */}
      {isSyncModalOpen && (
        <div className="fixed inset-0 w-screen h-screen z-[9999] flex items-center justify-center p-4 bg-black/70 backdrop-blur-md animate-in fade-in duration-300">
          <div className="absolute inset-0" onClick={() => setIsSyncModalOpen(false)}></div>
          
          <div className="relative w-full max-w-md bg-[#0f0f0f] border border-zinc-800 rounded-2xl shadow-[0_0_100px_rgba(0,0,0,0.9)] overflow-hidden scale-in-center animate-in zoom-in-95 duration-200">
            {/* Modal Header */}
            <div className="p-6 border-b border-zinc-800/50 flex justify-between items-start">
              <div className="flex flex-col gap-1">
                <h3 className="text-lg font-bold text-zinc-100 flex items-center gap-2">
                  <span className="p-2 bg-emerald-500/10 rounded-lg">
                    <FiMonitor className="w-5 h-5 text-emerald-500" />
                  </span>
                  VS Code Sync Hub
                </h3>
                <p className="text-xs text-zinc-500 font-medium">Yerel geliştirme ortamınızı buluta bağlayın.</p>
              </div>
              <button 
                onClick={() => setIsSyncModalOpen(false)}
                className="p-1.5 text-zinc-500 hover:text-zinc-200 hover:bg-zinc-800/50 rounded-lg transition-all"
              >
                <FiX className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-8 flex flex-col gap-6">
              <div className="space-y-3">
                <label className="text-[10px] font-black text-zinc-500 tracking-[0.2em] uppercase">Talimat</label>
                <p className="text-xs text-zinc-300 leading-relaxed">
                  Bu projeyi yerel VS Code ortamınızla eşitlemek ve dosyaları iki yönlü senkronize etmek için aşağıdaki komutu terminalinizde çalıştırın.
                </p>
              </div>

              <div className="relative group">
                <div className="absolute -inset-0.5 bg-gradient-to-r from-emerald-500/20 to-blue-500/20 rounded-xl blur opacity-30 group-hover:opacity-50 transition-all duration-500"></div>
                <div className="relative bg-black/40 border border-zinc-800 rounded-xl p-4 font-mono text-[11px] flex items-center justify-between gap-4">
                  <code className="text-emerald-400 tracking-tight">
                    <span className="text-zinc-600">currere-cli</span> connect <span className="text-zinc-600">--token</span> {syncToken}
                  </code>
                  <button 
                    onClick={() => copyToClipboard(`currere-cli connect --token ${syncToken}`)}
                    className="p-2 bg-zinc-800 hover:bg-emerald-500/20 text-zinc-400 hover:text-emerald-400 transition-all rounded-lg shrink-0 border border-zinc-700/50"
                  >
                    <FiCopy className="w-4 h-4" />
                  </button>
                </div>
              </div>

              <div className="bg-emerald-500/5 border border-emerald-500/10 rounded-xl p-4 flex items-center gap-4">
                <FiTerminal className="w-5 h-5 text-emerald-500/60" />
                <div className="flex flex-col gap-0.5">
                  <span className="text-[10px] text-emerald-500/80 font-bold uppercase tracking-widest leading-none">Bağlantı Türü</span>
                  <span className="text-xs text-zinc-400">WebSocket / SSH Tunneling</span>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-8 py-6 bg-zinc-900/30 border-t border-zinc-800/50 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
                </span>
                <span className="text-[10px] font-bold text-zinc-500 tracking-wider">YEREL BAĞLANTI BEKLENİYOR...</span>
              </div>
              <a 
                href="https://docs.currere.ai/cli" 
                target="_blank" 
                className="text-[10px] font-bold text-emerald-500 hover:text-emerald-400 transition-colors flex items-center gap-1.5"
              >
                CLI REHBERİ <FiExternalLink className="w-3 h-3" />
              </a>
            </div>
          </div>
        </div>
      )}

      {/* Hugging Face Push Modal */}
      {isPushModalOpen && (
        <div className="fixed inset-0 w-screen h-screen z-[9999] flex items-center justify-center p-4 bg-black/70 backdrop-blur-md animate-in fade-in duration-300">
          <div className="absolute inset-0" onClick={() => !isPushing && setIsPushModalOpen(false)}></div>
          
          <div className="relative w-full max-w-sm max-h-[85vh] overflow-y-auto bg-[#0f0f0f] border border-zinc-800 rounded-2xl shadow-[0_0_100px_rgba(0,0,0,0.9)] p-6 custom-scrollbar scale-in-center animate-in zoom-in-95 duration-200">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-zinc-100 font-bold flex items-center gap-2 tracking-tight">
                <SiHuggingface className="text-amber-400 w-5 h-5" />
                Hugging Face'e Aktar
              </h3>
              <button 
                onClick={() => setIsPushModalOpen(false)}
                className="text-zinc-500 hover:text-zinc-200 transition-colors"
                disabled={isPushing}
              >
                <FiX className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-3.5">
              <div>
                <label className="block text-[10px] font-black text-zinc-500 tracking-widest uppercase mb-1">Model / Repo Adı</label>
                <input 
                  type="text" 
                  value={repoName}
                  onChange={(e) => setRepoName(e.target.value)}
                  placeholder="örn: my-awesome-model"
                  className="w-full bg-black/40 border border-zinc-800 rounded-lg px-4 py-2 text-sm text-zinc-200 outline-none focus:border-amber-500/50 transition-all font-mono"
                />
              </div>

              <div>
                <label className="block text-[10px] font-black text-zinc-500 tracking-widest uppercase mb-1">Commit Mesajı</label>
                <input 
                  type="text" 
                  value={commitMsg}
                  onChange={(e) => setCommitMsg(e.target.value)}
                  className="w-full bg-black/40 border border-zinc-800 rounded-lg px-4 py-2 text-sm text-zinc-200 outline-none focus:border-amber-500/50 transition-all"
                />
              </div>

              <div className="flex items-center justify-between p-3 bg-zinc-900/50 border border-zinc-800 rounded-xl">
                 <div className="flex flex-col">
                   <span className="text-xs font-bold text-zinc-300">Gizlilik</span>
                   <span className="text-[10px] text-zinc-500">Sadece siz görebilirsiniz</span>
                 </div>
                 <button 
                   onClick={() => setIsPrivate(!isPrivate)}
                   className={`relative w-10 h-5 rounded-full transition-all duration-300 ${isPrivate ? 'bg-amber-500/20 border border-amber-500/40' : 'bg-zinc-800 border border-zinc-700'}`}
                 >
                   <div className={`absolute top-1 w-2.5 h-2.5 rounded-full transition-all duration-300 ${isPrivate ? 'right-1.5 bg-amber-500' : 'left-1.5 bg-zinc-500'}`} />
                 </button>
              </div>

              <div className="p-3 bg-emerald-500/5 border border-emerald-500/10 rounded-xl">
                <p className="text-[10px] text-zinc-400 leading-relaxed text-center">
                  <span className="font-bold text-emerald-500">{activeFile.name}</span> dosyası Hub'a yeni bir versiyon olarak yüklenecek.
                </p>
              </div>

              <button 
                onClick={handlePushToHub}
                disabled={isPushing}
                className="w-full bg-amber-500 hover:bg-amber-400 disabled:bg-zinc-800 text-black font-black py-3 rounded-xl text-xs tracking-widest transition-all shadow-lg shadow-amber-500/10 flex items-center justify-center gap-2 active:scale-95"
              >
                {isPushing ? (
                  <div className="w-4 h-4 border-2 border-black/30 border-t-black rounded-full animate-spin"></div>
                ) : <SiHuggingface className="w-4 h-4" />}
                {isPushing ? 'AKTIRILIYOR...' : 'AKTIRIMI BAŞLAT'}
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
}

