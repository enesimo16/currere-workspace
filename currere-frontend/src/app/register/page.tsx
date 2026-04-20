'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import api from '@/services/api';
import Link from 'next/link';
import axios from 'axios';

export default function RegisterPage() {
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  
  // Use an array to hold multiple validation error messages
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const router = useRouter();

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);
    setLoading(true);

    try {
      await api.post('/auth/register', { firstName, lastName, email, password });
      // On successful registration, redirect to login page
      router.push('/login');
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response) {
        // Handle .NET ValidationProblemDetails response
        const responseData = err.response.data;
        if (responseData && responseData.errors) {
          // Extract all validation errors from the 'errors' object keys
          const validationErrors: string[] = [];
          for (const key in responseData.errors) {
            if (Object.prototype.hasOwnProperty.call(responseData.errors, key)) {
              validationErrors.push(...responseData.errors[key]);
            }
          }
          setErrors(validationErrors);
        } else {
          // Fallback if there's a simple message string
          setErrors([responseData.message || 'Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.']);
        }
      } else {
        setErrors(['Beklenmeyen bir hata oluştu. Sunucuya bağlanılamıyor.']);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-zinc-50 relative overflow-hidden text-zinc-900">
      <div className="absolute top-0 left-0 w-full h-96 bg-gradient-to-b from-zinc-200/50 to-transparent -z-10 blur-3xl"></div>
      
      <div className="w-full max-w-md p-8 bg-white/80 backdrop-blur-xl rounded-3xl shadow-[0_8px_30px_rgb(0,0,0,0.04)] border border-zinc-100 transition-all duration-300">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-light tracking-tight text-zinc-800">Hesap Oluştur</h1>
          <p className="text-sm text-zinc-500 mt-2 font-light">Currere dünyasına katılın</p>
        </div>

        {errors.length > 0 && (
          <div className="mb-6 p-4 bg-red-50/80 backdrop-blur text-red-600 text-sm rounded-xl border border-red-100">
            <ul className="list-disc pl-5 space-y-1">
              {errors.map((err, idx) => (
                <li key={idx} className="font-medium">{err}</li>
              ))}
            </ul>
          </div>
        )}

        <form onSubmit={handleRegister} className="space-y-4">
          <div className="flex gap-4">
            <div className="space-y-1.5 w-1/2">
              <label className="block text-sm font-medium text-zinc-600 ml-1">Ad</label>
              <input
                type="text"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                className="w-full px-4 py-3 bg-zinc-50/50 border border-zinc-200 rounded-2xl focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-900/50 transition-all text-zinc-800"
                placeholder="Örn: Ahmet"
                required
              />
            </div>

            <div className="space-y-1.5 w-1/2">
              <label className="block text-sm font-medium text-zinc-600 ml-1">Soyad</label>
              <input
                type="text"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                className="w-full px-4 py-3 bg-zinc-50/50 border border-zinc-200 rounded-2xl focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-900/50 transition-all text-zinc-800"
                placeholder="Örn: Yılmaz"
                required
              />
            </div>
          </div>

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
            className="w-full py-3.5 px-4 bg-zinc-900 hover:bg-zinc-800 text-white rounded-2xl font-medium transition-all shadow-md hover:shadow-lg disabled:opacity-70 disabled:cursor-not-allowed mt-4"
          >
            {loading ? 'Kayıt Olunuyor...' : 'Kayıt Ol'}
          </button>
        </form>

        <div className="mt-8 text-center text-sm text-zinc-500">
          Zaten hesabınız var mı?{' '}
          <Link href="/login" className="font-medium text-zinc-800 hover:text-zinc-600 transition-colors">
            Giriş Yap
          </Link>
        </div>
      </div>
    </div>
  );
}
