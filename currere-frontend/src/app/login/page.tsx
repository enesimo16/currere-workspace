'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import api from '@/services/api';
import { useAuthStore } from '@/store/useAuthStore';
import Link from 'next/link';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const router = useRouter();
  const setToken = useAuthStore((state) => state.setToken);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await api.post('/auth/login', { email, password });
      
      if (response.data && response.data.token) {
        setToken(response.data.token);
        router.push('/dashboard');
      } else {
        setError('Giriş başarısız. Lütfen bilgilerinizi kontrol edin.');
      }
    } catch (err: unknown) {
      if (err && typeof err === 'object' && 'response' in err) {
        const error = err as { response?: { data?: { message?: string } } };
        setError(error.response?.data?.message || 'Bir hata oluştu. Sunucuya ulaşılamıyor.');
      } else {
        setError('Bir hata oluştu. Sunucuya ulaşılamıyor.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-zinc-50 relative overflow-hidden text-zinc-900">
      {/* Subtle background decoration */}
      <div className="absolute top-0 left-0 w-full h-96 bg-gradient-to-b from-zinc-200/50 to-transparent -z-10 blur-3xl"></div>
      
      <div className="w-full max-w-md p-8 bg-white/80 backdrop-blur-xl rounded-3xl shadow-[0_8px_30px_rgb(0,0,0,0.04)] border border-zinc-100 transition-all duration-300">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-light tracking-tight text-zinc-800">Tekrar Hoş Geldiniz</h1>
          <p className="text-sm text-zinc-500 mt-2 font-light">Hesabınıza giriş yapın</p>
        </div>

        {error && (
          <div className="mb-6 p-4 bg-red-50/80 backdrop-blur text-red-600 text-sm rounded-xl border border-red-100 font-medium">
            {error}
          </div>
        )}

        <form onSubmit={handleLogin} className="space-y-5">
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-zinc-600 ml-1">E-posta</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-4 py-3 bg-zinc-50/50 border border-zinc-200 rounded-2xl focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-900/50 transition-all text-zinc-800"
              placeholder="ornek@email.com"
              required
            />
          </div>

          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-zinc-600 ml-1">Şifre</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-4 py-3 bg-zinc-50/50 border border-zinc-200 rounded-2xl focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-900/50 transition-all text-zinc-800"
              placeholder="••••••••"
              required
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3.5 px-4 bg-zinc-900 hover:bg-zinc-800 text-white rounded-2xl font-medium transition-all shadow-md hover:shadow-lg disabled:opacity-70 disabled:cursor-not-allowed mt-2"
          >
            {loading ? 'Giriş Yapılıyor...' : 'Giriş Yap'}
          </button>
        </form>

        <div className="mt-8 text-center text-sm text-zinc-500">
          Hesabınız yok mu?{' '}
          <Link href="/register" className="font-medium text-zinc-800 hover:text-zinc-600 transition-colors">
            Kayıt Ol
          </Link>
        </div>
      </div>
    </div>
  );
}
