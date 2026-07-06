import axios from 'axios';
import { useAuthStore } from '../store/useAuthStore';
import toast from 'react-hot-toast';

// Create an Axios instance
const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL
    ? `${process.env.NEXT_PUBLIC_API_URL}/api`
    : 'http://localhost:5279/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to attach JWT token
api.interceptors.request.use(
  (config) => {
    const token = useAuthStore.getState().token;
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ── O-6 Fix: Global Response Interceptor ──────────────────────────────────
// Eski durum: 401/429 hataları komponent içinde sessizce yutuluyordu.
// Yeni durum: Merkezi interceptor tüm hataları yakalar ve uygun aksiyonu alır.
api.interceptors.response.use(
  (response) => response, // Başarılı yanıtları geçir
  (error) => {
    const status = error?.response?.status;

    if (status === 401) {
      // Oturum süresi dolmuş veya token geçersiz → kullanıcıyı login'e gönder
      // Store'u temizle
      useAuthStore.getState().logout?.();
      // Zaten login sayfasındaysak döngüye girme
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        toast.error('Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.', {
          id: 'session-expired', // Aynı anda birden fazla toast çıkmasın
          duration: 3000,
        });
        setTimeout(() => {
          window.location.href = '/login';
        }, 1500);
      }
    } else if (status === 429) {
      // Rate limit aşıldı — kullanıcıyı bilgilendir, uygulama çalışmaya devam etsin
      toast.error('İstek limiti aşıldı. Lütfen bir süre bekleyin.', {
        id: 'rate-limit', // Tekrar eden toast'ları dedupe et
        duration: 4000,
        icon: '🚦',
      });
    }

    return Promise.reject(error);
  }
);

export default api;

