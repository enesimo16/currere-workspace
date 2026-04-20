'use client';

import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

export default function EditorPage() {
  const { activeWorkspace } = useWorkspaceStore();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && !activeWorkspace) {
      router.push('/dashboard');
    }
  }, [mounted, activeWorkspace, router]);

  if (!mounted || !activeWorkspace) {
    return null;
  }

  return (
    <div className="min-h-screen bg-zinc-50 flex items-center justify-center p-6">
      <div className="bg-white p-10 rounded-3xl shadow-sm border border-zinc-200 text-center max-w-md w-full">
        <div className="w-16 h-16 bg-zinc-100 text-zinc-600 rounded-2xl flex items-center justify-center mx-auto mb-6">
          <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" /></svg>
        </div>
        <h1 className="text-2xl font-medium text-zinc-900 mb-2">Editor (Hazırlanıyor)</h1>
        <p className="text-zinc-500 font-light mb-6">Seçili Çalışma Alanı:</p>
        
        <div className="p-4 bg-zinc-50 border border-zinc-200 rounded-xl mb-6 text-left">
          <p className="text-sm font-medium text-zinc-800 mb-1">{activeWorkspace.name}</p>
          <p className="text-xs font-mono text-zinc-500 break-all">{activeWorkspace.id}</p>
        </div>

        <button 
          onClick={() => router.push('/dashboard')}
          className="px-5 py-2.5 bg-zinc-900 text-white rounded-xl font-medium text-sm hover:bg-zinc-800 transition-colors"
        >
          Dashboard&apos;a Dön
        </button>
      </div>
    </div>
  );
}
