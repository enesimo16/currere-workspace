import { useState, useEffect } from 'react';
import { Workspace, useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { FiEdit2, FiTrash2, FiBox, FiCheck, FiX, FiMonitor, FiCopy, FiTerminal, FiExternalLink, FiArrowLeft, FiDatabase, FiSearch, FiArchive, FiPlay } from 'react-icons/fi';
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
  const [commitMsg, setCommitMsg] = useState('Currere IDE üzerinden aktarıldı');
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
    <header className="h-14 border-b border-slate-300 flex items-center justify-between px-6 shrink-0 bg-white shadow-md shadow-black/10 z-40 relative">
      <div className="flex items-center">
        {/* Dashboard'a Dön Button */}
        <button
          onClick={() => router.push('/dashboard')}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-sm font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-100 transition-colors outline-none"
        >
          <FiArrowLeft className="w-4 h-4" />
          Dashboard
        </button>

        <div className="h-4 w-px bg-slate-200 mx-2"></div>
        
        <div className="flex items-center group ml-1">
          {isRenaming ? (
            <div className="flex items-center gap-1.5 animate-in fade-in slide-in-from-left-2 duration-200">
              <input
                autoFocus
                type="text"
                value={renameTitle}
                onChange={(e) => setRenameTitle(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleRenameSubmit();
                  if (e.key === 'Escape') setIsRenaming(false);
                }}
                className="bg-white border border-slate-300 rounded-md px-2 py-0.5 text-sm text-slate-800 outline-none w-48 focus:border-emerald-500 transition-colors font-bold"
              />
              <button onClick={handleRenameSubmit} className="p-1 text-emerald-500 hover:bg-emerald-50 rounded-md transition-colors"><FiCheck className="w-4 h-4" /></button>
              <button onClick={() => setIsRenaming(false)} className="p-1 text-slate-400 hover:bg-slate-100 rounded-md transition-colors"><FiX className="w-4 h-4" /></button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <span className="text-slate-900 font-bold text-lg tracking-tight">
                {activeWorkspace.title || activeWorkspace.name || 'İsimsiz Proje'}
              </span>
              <span className="ml-3 bg-slate-100 text-slate-500 text-[11px] px-2 py-0.5 rounded-full font-medium uppercase tracking-wider">
                Sandbox
              </span>
              
              <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity ml-1">
                <button onClick={() => setIsRenaming(true)} className="p-1.5 text-slate-400 hover:text-slate-600 transition-colors rounded-md hover:bg-slate-100"><FiEdit2 className="w-3.5 h-3.5" /></button>
                <button onClick={handleDelete} className="p-1.5 text-slate-400 hover:text-red-500 transition-colors rounded-md hover:bg-red-50"><FiTrash2 className="w-3.5 h-3.5" /></button>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center gap-3">
        {/* VS Code Connect Button */}
        <button
          onClick={openSyncModal}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-[13px] font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-200 transition-colors duration-200"
        >
          <FiMonitor className="w-4 h-4" />
          VS Code'a Bağlan
        </button>

        <button
          onClick={() => setIsPushModalOpen(true)}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-[13px] font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-200 transition-colors duration-200"
        >
          <SiHuggingface className="w-4 h-4" />
          Push to Hub
        </button>

        <div className="w-px h-5 bg-slate-200 mx-1"></div>

        <button
          onClick={() => useWorkspaceStore.getState().setSyntheticOpen(true)}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-[13px] font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-200 transition-colors duration-200"
          title="Sentetik Veri Üret"
        >
          <FiDatabase className="w-4 h-4" />
          <span className="hidden lg:inline">Veri</span>
        </button>

        <button
          onClick={() => useWorkspaceStore.getState().setKaggleOpen(true)}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-[13px] font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-200 transition-colors duration-200"
          title="Kaggle Dataset Arama"
        >
          <FiSearch className="w-4 h-4" />
          <span className="hidden lg:inline">Kaggle</span>
        </button>

        <button
          onClick={() => useWorkspaceStore.getState().setHistoryOpen(true)}
          className="flex items-center gap-2 px-3 py-1.5 rounded-md text-[13px] font-medium text-slate-500 hover:text-slate-900 hover:bg-slate-200 transition-colors duration-200"
          title="Snapshot Geçmişi"
        >
          <FiArchive className="w-4 h-4" />
          <span className="hidden lg:inline">Yedekler</span>
        </button>

        <div className="w-px h-5 bg-slate-200 mx-1"></div>

        <button
          onClick={onRun}
          disabled={isExecuting}
          className="flex items-center gap-2 bg-zinc-800 hover:bg-zinc-700 disabled:bg-zinc-800/50 disabled:text-zinc-500 disabled:cursor-not-allowed text-zinc-100 px-4 py-1.5 rounded-md text-[13px] font-medium transition-colors duration-200 shadow-sm"
        >
          {isExecuting ? (
            <div className="w-4 h-4 border-2 border-zinc-100/40 border-t-zinc-100 rounded-full animate-spin"></div>
          ) : (
            <FiPlay className="w-4 h-4" />
          )}
          {isExecuting ? 'Yürütülüyor...' : 'Kodu Çalıştır'}
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
                  <span className="p-2 bg-zinc-800 rounded-lg border border-zinc-700">
                    <FiMonitor className="w-5 h-5 text-zinc-400" />
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
                <div className="absolute -inset-0.5 bg-gradient-to-r from-zinc-500/10 to-zinc-600/10 rounded-xl blur opacity-30 group-hover:opacity-50 transition-all duration-500"></div>
                <div className="relative bg-black/40 border border-zinc-800 rounded-xl p-4 font-mono text-[11px] flex items-center justify-between gap-4">
                  <code className="text-zinc-300 tracking-tight">
                    <span className="text-zinc-600">currere-cli</span> connect <span className="text-zinc-600">--token</span> {syncToken}
                  </code>
                  <button 
                    onClick={() => copyToClipboard(`currere-cli connect --token ${syncToken}`)}
                    className="p-2 bg-zinc-800 hover:bg-zinc-700 text-zinc-400 hover:text-zinc-200 transition-all rounded-lg shrink-0 border border-zinc-700/50"
                  >
                    <FiCopy className="w-4 h-4" />
                  </button>
                </div>
              </div>

              <div className="bg-zinc-800/30 border border-zinc-700/50 rounded-xl p-4 flex items-center gap-4">
                <FiTerminal className="w-5 h-5 text-zinc-500" />
                <div className="flex flex-col gap-0.5">
                  <span className="text-[10px] text-zinc-400 font-bold uppercase tracking-widest leading-none">Bağlantı Türü</span>
                  <span className="text-xs text-zinc-500">WebSocket / SSH Tunneling</span>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-8 py-6 bg-zinc-900/30 border-t border-zinc-800/50 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-zinc-500 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-zinc-400"></span>
                </span>
                <span className="text-[10px] font-bold text-zinc-500 tracking-wider">YEREL BAĞLANTI BEKLENİYOR...</span>
              </div>
              <a 
                href="https://docs.currere.ai/cli" 
                target="_blank" 
                className="text-[10px] font-bold text-zinc-400 hover:text-zinc-300 transition-colors flex items-center gap-1.5"
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
                <SiHuggingface className="text-zinc-300 w-5 h-5" />
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
                  className="w-full bg-black/40 border border-zinc-800 rounded-lg px-4 py-2 text-sm text-zinc-200 outline-none focus:border-zinc-500 transition-all font-mono"
                />
              </div>

              <div>
                <label className="block text-[10px] font-black text-zinc-500 tracking-widest uppercase mb-1">Commit Mesajı</label>
                <input 
                  type="text" 
                  value={commitMsg}
                  onChange={(e) => setCommitMsg(e.target.value)}
                  className="w-full bg-black/40 border border-zinc-800 rounded-lg px-4 py-2 text-sm text-zinc-200 outline-none focus:border-zinc-500 transition-all"
                />
              </div>

              <div className="flex items-center justify-between p-3 bg-zinc-900/50 border border-zinc-800 rounded-xl">
                 <div className="flex flex-col">
                   <span className="text-xs font-bold text-zinc-300">Gizlilik</span>
                   <span className="text-[10px] text-zinc-500">Sadece siz görebilirsiniz</span>
                 </div>
                 <button 
                   onClick={() => setIsPrivate(!isPrivate)}
                   className={`relative w-10 h-5 rounded-full transition-all duration-300 ${isPrivate ? 'bg-zinc-600 border border-zinc-500' : 'bg-zinc-800 border border-zinc-700'}`}
                 >
                   <div className={`absolute top-1 w-2.5 h-2.5 rounded-full transition-all duration-300 ${isPrivate ? 'right-1.5 bg-zinc-200' : 'left-1.5 bg-zinc-500'}`} />
                 </button>
              </div>

              <div className="p-3 bg-zinc-800/30 border border-zinc-700/50 rounded-xl">
                <p className="text-[10px] text-zinc-400 leading-relaxed text-center">
                  <span className="font-bold text-zinc-300">{activeFile.name}</span> dosyası Hub'a yeni bir versiyon olarak yüklenecek.
                </p>
              </div>

              <button 
                onClick={handlePushToHub}
                disabled={isPushing}
                className="w-full bg-zinc-800 hover:bg-zinc-700 disabled:bg-zinc-900 text-zinc-100 font-bold py-3 rounded-xl text-xs tracking-widest transition-all shadow-md border border-zinc-700/50 flex items-center justify-center gap-2 active:scale-95"
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

