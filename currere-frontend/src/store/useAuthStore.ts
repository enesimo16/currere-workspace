import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface AuthState {
  token: string | null;
  huggingFaceToken: string | null;
  isAuthenticated: boolean;
  setToken: (token: string) => void;
  setHuggingFaceToken: (hfToken: string | null) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      huggingFaceToken: null,
      isAuthenticated: false,
      setToken: (token: string) => set({ token, isAuthenticated: true }),
      setHuggingFaceToken: (huggingFaceToken: string | null) => set({ huggingFaceToken }),
      logout: () => set({ token: null, huggingFaceToken: null, isAuthenticated: false }),
    }),
    {
      name: 'auth-storage',
    }
  )
);
