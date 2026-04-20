'use client';

import { useAuthStore } from '@/store/useAuthStore';
import { useWorkspaceStore, Workspace } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import api from '@/services/api';
import axios from 'axios';

export default function DashboardPage() {
  const { isAuthenticated, token, logout } = useAuthStore();
  const { setActiveWorkspace } = useWorkspaceStore();
  const router = useRouter();
  
  const [mounted, setMounted] = useState(false);
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  
  // Create a default string to avoid reference checks on simple strings if needed
  const loadWorkspaces = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await api.get('/workspace');
      // Assume res.data is an array or has a data property
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
      }
    }
  }, [isAuthenticated, token, router, mounted, loadWorkspaces]);

  const handleCreateWorkspace = async () => {
    const name = prompt('Yeni çalışma alanı adı:', `Yeni Workspace ${Math.floor(Math.random() * 1000)}`);
    if (!name || !name.trim()) return;
    
    try {
      setIsCreating(true);
      await api.post('/workspace', { title: name.trim() });
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

  const handleDeleteWorkspace = async (id: string | number, e: React.MouseEvent) => {
    e.stopPropagation(); // Prevent card click
    if (!confirm('Bu çalışma alanını silmek istediğinize emin misiniz?')) return;
    
    try {
      await api.delete(`/workspace/${id}`);
      setWorkspaces(prev => prev.filter(w => w.id !== id));
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
          <div className="w-8 h-8 bg-zinc-900 text-white rounded-lg flex items-center justify-center font-bold text-lg">C</div>
          <span className="text-xl font-medium tracking-tight">Currere</span>
        </div>
        <button
          onClick={() => {
            logout();
            router.push('/login');
          }}
          className="text-sm font-medium text-zinc-600 hover:text-zinc-900 transition-colors"
        >
          Çıkış Yap
        </button>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-6xl w-full mx-auto p-6 md:p-8">
        <div className="flex justify-between items-end mb-8">
          <div>
            <h1 className="text-3xl font-light tracking-tight text-zinc-800">Çalışma Alanları</h1>
            <p className="text-zinc-500 mt-1 font-light">Tüm projelerinizi buradan yönetebilirsiniz.</p>
          </div>
          <button
            onClick={handleCreateWorkspace}
            disabled={isCreating}
            className="flex items-center gap-2 bg-zinc-900 hover:bg-zinc-800 text-white px-5 py-2.5 rounded-xl font-medium transition-all shadow-sm hover:shadow-md disabled:opacity-70 disabled:cursor-not-allowed"
          >
            {isCreating ? (
              <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
            ) : (
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" /></svg>
            )}
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
              onClick={handleCreateWorkspace}
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
                className="group cursor-pointer bg-white border border-gray-200 rounded-2xl p-5 hover:border-gray-300 hover:shadow-md hover:-translate-y-1 transition-all flex flex-col justify-between h-48 relative overflow-hidden"
              >
                {/* Subtle top color bar */}
                <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-zinc-200 to-zinc-300 group-hover:from-blue-400 group-hover:to-indigo-500 transition-all"></div>
                
                <div className="mt-2">
                  <div className="flex items-start justify-between">
                    <h3 className="text-lg font-medium text-zinc-800 line-clamp-2 pr-8">{workspace.title || workspace.name || 'Yeni Çalışma Alanı'}</h3>
                    <button
                      onClick={(e) => handleDeleteWorkspace(workspace.id, e)}
                      className="text-zinc-400 hover:text-red-500 hover:bg-red-50 p-2 rounded-xl transition-all absolute top-4 right-4 opacity-0 group-hover:opacity-100"
                      title="Sil"
                    >
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
                    </button>
                  </div>
                </div>
                
                <div className="flex justify-between items-center pt-4 border-t border-zinc-100">
                  <span className="text-xs font-medium text-zinc-500 uppercase tracking-wider">Workspace</span>
                  <div className="w-8 h-8 rounded-full bg-zinc-50 border border-zinc-200 flex items-center justify-center text-zinc-600 group-hover:bg-zinc-900 group-hover:text-white group-hover:border-zinc-900 transition-colors">
                    <svg className="w-4 h-4 ml-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" /></svg>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
