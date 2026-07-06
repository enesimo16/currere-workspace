'use client';

import { useAuthStore } from '@/store/useAuthStore';
import { useWorkspaceStore, Workspace } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import { FiAperture, FiLogOut, FiTrash2, FiSettings, FiX, FiZap } from 'react-icons/fi';
import api from '@/services/api';
import axios from 'axios';
import toast from 'react-hot-toast';

export default function DashboardPage() {
  const { isAuthenticated, token, logout } = useAuthStore();
  const { setActiveWorkspace } = useWorkspaceStore();
  const router = useRouter();
  
  const [mounted, setMounted] = useState(false);
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [isCreatingModalOpen, setIsCreatingModalOpen] = useState(false);
  const [newWorkspaceTitle, setNewWorkspaceTitle] = useState('');
  const [newWorkspaceFormat, setNewWorkspaceFormat] = useState(1); // 1 = Python
  const [newWorkspaceRuntime, setNewWorkspaceRuntime] = useState(1); // 1 = CPU
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [workspaceToDelete, setWorkspaceToDelete] = useState<Workspace | null>(null);

  // Settings State
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [kaggleUsername, setKaggleUsername] = useState('');
  const [kaggleKey, setKaggleKey] = useState('');
  const [isKaggleConfigured, setIsKaggleConfigured] = useState(false);
  const [isHfConfigured, setIsHfConfigured] = useState(false);
  const [hfTokenInput, setHfTokenInput] = useState('');
  const [isSavingSettings, setIsSavingSettings] = useState(false);

  // Temporary User Profile State (To be replaced with real auth data)
  const userName = "Enes Yel";
  const userInitials = "EY";

  const loadWorkspaces = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await api.get('/workspace');
      const data = Array.isArray(res.data) ? res.data : (res.data?.data || []);
      setWorkspaces(data);
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        setError(err.response?.data?.message || 'Çalışma alanları yüklenirken bir hata oluştu.');
        if (err.response?.status === 401) {
          logout();
          router.push('/login');
        }
      } else {
        setError('Çalışma alanları yüklenirken bir hata oluştu.');
      }
    } finally {
      setLoading(false);
    }
  }, [logout, router]);

  const loadSettings = useCallback(async () => {
    try {
      const kaggleRes = await api.get('/user/settings/kaggle');
      if (kaggleRes.data) {
        setIsKaggleConfigured(kaggleRes.data.isConfigured);
        if (kaggleRes.data.username) setKaggleUsername(kaggleRes.data.username);
      }
      
      const hfRes = await api.get('/user/settings/huggingface');
      if (hfRes.data) {
        setIsHfConfigured(hfRes.data.isConfigured);
      }
    } catch (err) {
      console.error('Ayarlar yüklenemedi:', err);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted) {
      if (!isAuthenticated || !token) {
        router.push('/login');
      } else {
        // eslint-disable-next-line react-hooks/set-state-in-effect
        loadWorkspaces();
        loadSettings();
      }
    }
  }, [isAuthenticated, token, router, mounted, loadWorkspaces, loadSettings]);

  const handleSaveSettings = async () => {
    try {
      setIsSavingSettings(true);
      if (!isKaggleConfigured && kaggleUsername && kaggleKey) {
        await api.post('/user/settings/kaggle', { username: kaggleUsername, key: kaggleKey });
      }
      if (!isHfConfigured && hfTokenInput) {
        await api.post('/user/settings/huggingface', { token: hfTokenInput });
        useAuthStore.getState().setHuggingFaceToken(hfTokenInput);
      }
      
      await loadSettings();
      setIsSettingsOpen(false);
      toast.success('Ayarlar başarıyla kaydedildi');
    } catch {
      toast.error('Ayarlar kaydedilirken hata oluştu');
    } finally {
      setIsSavingSettings(false);
    }
  };

  const handleCreateWorkspace = async () => {
    if (!newWorkspaceTitle.trim()) return;
    
    try {
      setIsCreating(true);
      await api.post('/workspace', { 
        title: newWorkspaceTitle.trim(),
        format: newWorkspaceFormat,
        runtime: newWorkspaceRuntime
      });
      setIsCreatingModalOpen(false);
      setNewWorkspaceTitle('');
      await loadWorkspaces();
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        alert(err.response?.data?.message || 'Çalışma alanı oluşturulamadı.');
      } else {
        alert('Çalışma alanı oluşturulamadı.');
      }
    } finally {
      setIsCreating(false);
    }
  };

  const handleDeleteWorkspace = (workspace: Workspace, e: React.MouseEvent) => {
    e.stopPropagation();
    setWorkspaceToDelete(workspace);
    setIsDeleteModalOpen(true);
  };

  const confirmDelete = async () => {
    if (!workspaceToDelete) return;
    try {
      await api.delete(`/workspace/${workspaceToDelete.id}`);
      setWorkspaces(prev => prev.filter(w => w.id !== workspaceToDelete.id));
      setIsDeleteModalOpen(false);
      setWorkspaceToDelete(null);
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        alert(err.response?.data?.message || 'Silme işlemi başarısız.');
      } else {
        alert('Silme işlemi başarısız.');
      }
    }
  };

  const handleCardClick = (workspace: Workspace) => {
    setActiveWorkspace(workspace);
    router.push('/editor');
  };

  if (!mounted || !isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-50">
        <div className="w-8 h-8 border-4 border-zinc-200 border-t-zinc-900 rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 flex flex-col font-sans">
      {/* Navbar */}
      <header className="px-6 py-4 bg-white border-b border-zinc-200 flex justify-between items-center sticky top-0 z-10 shadow-sm">
        <div className="flex items-center gap-2">
          <div className="bg-zinc-900 text-white p-1.5 rounded-lg shadow-sm w-8 h-8 flex items-center justify-center">
            <FiAperture className="w-5 h-5" />
          </div>
          <span className="text-xl font-semibold tracking-tight text-zinc-900">Currere</span>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-zinc-200 text-zinc-700 flex items-center justify-center text-xs font-bold">
              {userInitials}
            </div>
            <span className="text-sm font-medium text-zinc-700 hidden sm:inline">{userName}</span>
          </div>
          <div className="h-4 w-px bg-zinc-300"></div>
          <button
            onClick={() => setIsSettingsOpen(true)}
            className="text-zinc-500 hover:text-indigo-600 hover:bg-indigo-50 px-2 py-1.5 rounded-md transition-colors flex items-center gap-2 text-sm"
            title="Ayarlar & Entegrasyonlar"
          >
            <FiSettings className="w-4 h-4" />
            <span className="hidden sm:inline">Entegrasyonlar</span>
          </button>
          <button
            onClick={() => {
              logout();
              router.push('/login');
            }}
            className="text-zinc-500 hover:text-red-600 hover:bg-red-50 px-2 py-1.5 rounded-md transition-colors flex items-center gap-2 text-sm"
            title="Çıkış Yap"
          >
            <FiLogOut className="w-4 h-4" />
            <span className="hidden sm:inline">Çıkış</span>
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-6xl w-full mx-auto p-6 md:p-8">
        <div className="flex justify-between items-end mb-8">
          <div>
            <h1 className="text-3xl font-light tracking-tight text-zinc-800">Çalışma Alanları</h1>
            <p className="text-zinc-500 mt-1 font-light">Tüm projelerinizi buradan yönetebilirsiniz.</p>
          </div>
          <button
            onClick={() => {
              setNewWorkspaceTitle(`Yeni Workspace ${Math.floor(Math.random() * 1000)}`);
              setIsCreatingModalOpen(true);
            }}
            disabled={isCreating}
            className="flex items-center gap-2 bg-zinc-900 hover:bg-zinc-800 text-white px-5 py-2.5 rounded-xl font-medium shadow-md hover:shadow-lg transition-all disabled:opacity-70 disabled:cursor-not-allowed"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" /></svg>
            Yeni Çalışma Alanı
          </button>
        </div>

        {error && (
          <div className="mb-6 p-4 bg-red-50 text-red-600 rounded-xl border border-red-100 font-medium">
            {error}
          </div>
        )}

        {loading ? (
          <div className="flex justify-center py-20">
            <div className="w-8 h-8 border-4 border-zinc-200 border-t-zinc-900 rounded-full animate-spin"></div>
          </div>
        ) : workspaces.length === 0 ? (
          <div className="text-center py-20 bg-white rounded-3xl border border-zinc-200 border-dashed">
            <div className="w-16 h-16 bg-zinc-100 text-zinc-400 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 002-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" /></svg>
            </div>
            <h3 className="text-lg font-medium text-zinc-800">Henüz hiçbir çalışma alanınız yok</h3>
            <p className="text-zinc-500 mt-2 mb-6 max-w-sm mx-auto font-light">Yeni bir proje başlatmak için hemen bir çalışma alanı oluşturun.</p>
            <button
              onClick={() => {
                setNewWorkspaceTitle(`Yeni Workspace ${Math.floor(Math.random() * 1000)}`);
                setIsCreatingModalOpen(true);
              }}
              className="px-5 py-2.5 bg-white border border-zinc-200 hover:border-zinc-300 text-zinc-800 rounded-xl font-medium transition-all shadow-sm"
            >
              İlk Alanınızı Oluşturun
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            {workspaces.map((workspace) => (
              <div
                key={workspace.id}
                onClick={() => handleCardClick(workspace)}
                className="group cursor-pointer transition-all duration-300 hover:-translate-y-1 hover:shadow-xl hover:border-zinc-300 border border-zinc-200 bg-white rounded-xl p-5 flex flex-col justify-between h-48 relative overflow-hidden"
              >
                {/* Subtle top color bar */}
                <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-zinc-200 to-zinc-300 group-hover:from-blue-400 group-hover:to-indigo-500 transition-all"></div>
                
                <div className="mt-2">
                  <div className="flex items-start justify-between">
                    <h3 className="text-lg font-medium text-zinc-800 line-clamp-2 pr-8">{workspace.title || workspace.name || 'Yeni Çalışma Alanı'}</h3>
                    <button
                      onClick={(e) => handleDeleteWorkspace(workspace, e)}
                      className="text-zinc-400 hover:text-red-600 bg-white border border-zinc-100 hover:border-red-200 hover:bg-red-50/30 p-2 rounded-lg shadow-sm transition-all absolute top-4 right-4 opacity-0 group-hover:opacity-100"
                      title="Sil"
                    >
                      <FiTrash2 className="w-4 h-4" />
                    </button>
                  </div>
                </div>
                
                <div className="flex justify-between items-center pt-4 border-t border-zinc-100">
                  <div className="flex flex-col">
                    <span className="text-xs font-medium text-zinc-500 uppercase tracking-wider">Workspace</span>
                    <span className="text-[11px] text-zinc-400 mt-0.5">
                      {workspace.createdAt ? `Oluşturulma: ${new Date(workspace.createdAt).toLocaleDateString('tr-TR')}` : 'Son güncelleme: Bugün'}
                    </span>
                  </div>
                  <div className="w-8 h-8 rounded-full flex items-center justify-center text-zinc-400 group-hover:bg-zinc-100 group-hover:text-zinc-900 transition-colors">
                    <svg className="w-4 h-4 ml-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" /></svg>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
      
      {/* Create Workspace Modal */}
      {isCreatingModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-zinc-900/60 backdrop-blur-sm">
          <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden border border-zinc-200 animate-in fade-in zoom-in duration-200">
            <div className="p-6 border-b border-zinc-100 flex justify-between items-center bg-zinc-50/50">
              <h2 className="text-xl font-medium text-zinc-800">Yeni Çalışma Alanı</h2>
              <button onClick={() => setIsCreatingModalOpen(false)} className="text-zinc-400 hover:text-zinc-600">
                <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>
            
            <div className="p-6 space-y-6">
              <div>
                <label className="block text-sm font-medium text-zinc-700 mb-2">Başlık</label>
                <input 
                  autoFocus
                  type="text" 
                  value={newWorkspaceTitle}
                  onChange={(e) => setNewWorkspaceTitle(e.target.value)}
                  className="w-full px-4 py-3 bg-zinc-50 border border-zinc-200 rounded-2xl outline-none focus:border-zinc-950 transition-colors"
                  placeholder="Proje adını girin..."
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-zinc-700 mb-2">Format</label>
                  <select 
                    value={newWorkspaceFormat}
                    onChange={(e) => setNewWorkspaceFormat(Number(e.target.value))}
                    className="w-full px-4 py-3 bg-zinc-50 border border-zinc-200 rounded-2xl outline-none focus:border-zinc-950 transition-colors"
                  >
                    <option value={1}>Python (.py)</option>
                    <option value={2}>Notebook (.ipynb)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-zinc-700 mb-2">Runtime</label>
                  <select 
                    value={newWorkspaceRuntime}
                    onChange={(e) => setNewWorkspaceRuntime(Number(e.target.value))}
                    className="w-full px-4 py-3 bg-zinc-50 border border-zinc-200 rounded-2xl outline-none focus:border-zinc-950 transition-colors"
                  >
                    <option value={1}>CPU</option>
                    <option value={2}>GPU</option>
                  </select>
                </div>
              </div>
              
              <div className="pt-2">
                <button
                  onClick={handleCreateWorkspace}
                  disabled={isCreating || !newWorkspaceTitle.trim()}
                  className="w-full bg-zinc-900 hover:bg-zinc-800 text-white py-3.5 rounded-2xl font-medium transition-all shadow-md active:scale-[0.98] disabled:opacity-70 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                >
                  {isCreating && <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>}
                  {isCreating ? 'Oluşturuluyor...' : 'Hemen Başla'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
      {/* Delete Confirmation Modal */}
      {isDeleteModalOpen && workspaceToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-900/40 backdrop-blur-sm">
          <div className="bg-white p-6 rounded-2xl shadow-xl border border-zinc-100 max-w-sm w-full mx-4 transform transition-all animate-in fade-in zoom-in duration-200">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-red-50 flex items-center justify-center text-red-500 shrink-0">
                <FiTrash2 className="w-5 h-5" />
              </div>
              <h2 className="text-lg font-semibold text-zinc-900">Çalışma Alanını Sil</h2>
            </div>
            
            <p className="text-sm text-zinc-500 mb-6 leading-relaxed">
              <strong className="font-medium text-zinc-700">{workspaceToDelete.title || workspaceToDelete.name}</strong> adlı çalışma alanını silmek istediğinize emin misiniz? Bu işlem geri alınamaz ve içindeki tüm veriler kaybolur.
            </p>
            
            <div className="flex justify-end gap-3">
              <button 
                onClick={() => {
                  setIsDeleteModalOpen(false);
                  setWorkspaceToDelete(null);
                }}
                className="px-4 py-2 text-sm font-medium text-zinc-600 bg-zinc-100 hover:bg-zinc-200 rounded-lg transition-colors"
              >
                İptal
              </button>
              <button 
                onClick={confirmDelete}
                className="px-4 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-700 rounded-lg shadow-sm transition-colors"
              >
                Sil
              </button>
            </div>
          </div>
        </div>
      )}
      {/* Settings / Integrations Modal */}
      {isSettingsOpen && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-zinc-900/40 backdrop-blur-sm px-4">
          <div className="w-full max-w-sm max-h-[85vh] overflow-y-auto bg-white border border-zinc-200 rounded-2xl p-6 shadow-2xl custom-scrollbar animate-in zoom-in-95 duration-200">
            <div className="flex justify-between items-center mb-5">
              <h3 className="text-zinc-900 font-bold tracking-wide flex items-center gap-2 text-lg">
                <FiSettings className="text-zinc-500" />
                Global Entegrasyonlar
              </h3>
              <button onClick={() => setIsSettingsOpen(false)} className="text-zinc-500 hover:text-zinc-800 transition cursor-pointer p-1 hover:bg-zinc-100 rounded-md">
                <FiX className="w-5 h-5"/>
              </button>
            </div>
            
            <div className="space-y-4">
               {isKaggleConfigured ? (
                 <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
                   <p className="text-emerald-700 text-sm font-bold flex items-center gap-2">
                     <FiZap /> Kaggle Hesabınız Bağlı
                   </p>
                   <p className="text-zinc-600 text-xs mt-1">Username: {kaggleUsername}</p>
                   <button onClick={() => setIsKaggleConfigured(false)} className="text-emerald-600 hover:text-emerald-700 text-xs mt-2 underline font-medium">Farklı bir hesap bağla</button>
                 </div>
               ) : (
                 <>
                   <div>
                      <label className="block text-[11px] text-zinc-500 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE USERNAME</label>
                      <input type="text" value={kaggleUsername} onChange={e => setKaggleUsername(e.target.value)} className="w-full bg-zinc-50 border border-zinc-200 rounded-xl px-4 py-2.5 text-sm text-zinc-900 outline-none focus:border-zinc-900 focus:ring-1 focus:ring-zinc-900/50 transition-all shadow-sm" placeholder="Kaggle kullanıcı adınız" />
                   </div>
                    <div>
                      <label className="block text-[11px] text-zinc-500 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE API KEY</label>
                      <input type="password" value={kaggleKey} onChange={e => setKaggleKey(e.target.value)} className="w-full bg-zinc-50 border border-zinc-200 rounded-xl px-4 py-2.5 text-sm text-zinc-900 outline-none focus:border-zinc-900 focus:ring-1 focus:ring-zinc-900/50 transition-all shadow-sm" placeholder="Kaggle API Key" />
                   </div>
                 </>
               )}
               <div className="pt-4 border-t border-zinc-100">
                  <label className="block text-[11px] text-zinc-500 mb-1.5 tracking-wider font-semibold uppercase flex items-center gap-2">
                    <span className="w-1.5 h-1.5 bg-zinc-500 rounded-full"></span>
                    Hugging Face Access Token
                  </label>
                  
                  {isHfConfigured ? (
                    <div className="p-4 bg-blue-50 border border-blue-200 rounded-xl">
                      <p className="text-blue-700 text-sm font-bold flex items-center gap-2">
                        <FiZap /> Hugging Face Hesabınız Bağlı
                      </p>
                      <button onClick={() => setIsHfConfigured(false)} className="text-blue-600 hover:text-blue-700 text-xs mt-2 underline font-medium">Farklı bir hesap bağla</button>
                    </div>
                  ) : (
                    <>
                      <input 
                        type="password" 
                        value={hfTokenInput} 
                        onChange={e => setHfTokenInput(e.target.value)} 
                        className="w-full bg-zinc-50 border border-zinc-200 rounded-xl px-4 py-2.5 text-sm text-zinc-900 outline-none focus:border-zinc-900 focus:ring-1 focus:ring-zinc-900/50 transition-all shadow-sm" 
                        placeholder="hf_..." 
                      />
                      <p className="text-[10px] text-zinc-500 mt-2 leading-relaxed">
                        Model Hub'a aktarım yapmak için <b className="text-zinc-700">Write</b> yetkili bir token gereklidir.
                      </p>
                    </>
                  )}
               </div>
               <button disabled={isSavingSettings} onClick={handleSaveSettings} className="w-full mt-2 bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-white rounded-xl py-3 text-sm font-bold transition-colors disabled:opacity-50 shadow-md">
                  {isSavingSettings ? 'Kaydediliyor...' : 'AYARLARI KAYDET'}
               </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
