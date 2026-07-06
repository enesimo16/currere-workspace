/**
 * useFileCache — localStorage Tabanlı Kalıcı Dosya İçeriği Cache'i
 *
 * Amacı: Kullanıcı sayfayı yenilediğinde veya sekmeyi kapatıp açtığında
 *         yazdığı kod kaybolmadan korunsun. Backend henüz yüklenmeden önce
 *         UI'da son bilinen içerik gösterilir (stale-while-revalidate pattern).
 *
 * Cache anahtarı: `fc:{workspaceId}:{fileName}` (örn: "fc:42:main.py")
 * Cache TTL: Kalıcı dosyalar için 7 gün, geçici dosyalar için 4 saat.
 *             Backend'den taze veri gelince cache otomatik güncellenir.
 */
'use client';

import { useCallback } from 'react';

const CACHE_PREFIX = 'fc:';
const PERMANENT_TTL_MS = 7 * 24 * 60 * 60 * 1000;  // 7 gün
const TEMPORARY_TTL_MS = 4 * 60 * 60 * 1000;        // 4 saat

const PERMANENT_EXTENSIONS = new Set([
  '.py', '.ipynb', '.js', '.ts', '.jsx', '.tsx',
  '.cs', '.java', '.cpp', '.c', '.h',
  '.md', '.rst', '.sql', '.r', '.rb',
]);

interface CacheEntry {
  content: string;
  timestamp: number;
  ttl: number;
}

function isPermanentFile(fileName: string): boolean {
  const ext = '.' + (fileName.split('.').pop()?.toLowerCase() ?? '');
  return PERMANENT_EXTENSIONS.has(ext);
}

function buildKey(workspaceId: string | number, fileName: string): string {
  return `${CACHE_PREFIX}${workspaceId}:${fileName}`;
}

export function useFileCache(workspaceId: string | number | undefined) {
  /**
   * Cache'den okur. Eğer kayıt yoksa veya süresi dolmuşsa null döner.
   */
  const readCache = useCallback((fileName: string): string | null => {
    if (!workspaceId || typeof window === 'undefined') return null;
    try {
      const raw = localStorage.getItem(buildKey(workspaceId, fileName));
      if (!raw) return null;

      const entry: CacheEntry = JSON.parse(raw);
      const isExpired = Date.now() - entry.timestamp > entry.ttl;

      if (isExpired) {
        localStorage.removeItem(buildKey(workspaceId, fileName));
        return null;
      }

      return entry.content;
    } catch {
      return null;
    }
  }, [workspaceId]);

  /**
   * Cache'e yazar. Permanent dosyalar 7 gün, geçici dosyalar 4 saat saklanır.
   */
  const writeCache = useCallback((fileName: string, content: string): void => {
    if (!workspaceId || typeof window === 'undefined') return;
    try {
      const ttl = isPermanentFile(fileName) ? PERMANENT_TTL_MS : TEMPORARY_TTL_MS;
      const entry: CacheEntry = {
        content,
        timestamp: Date.now(),
        ttl,
      };
      localStorage.setItem(buildKey(workspaceId, fileName), JSON.stringify(entry));
    } catch (err) {
      // localStorage dolu veya devre dışıysa sessizce devam et
      console.warn('[useFileCache] localStorage yazma hatası:', err);
    }
  }, [workspaceId]);

  /**
   * Belirli bir dosyanın cache kaydını siler (örn: dosya silindiğinde).
   */
  const clearCache = useCallback((fileName: string): void => {
    if (!workspaceId || typeof window === 'undefined') return;
    try {
      localStorage.removeItem(buildKey(workspaceId, fileName));
    } catch { /* sessiz */ }
  }, [workspaceId]);

  /**
   * Bu workspace'e ait tüm cache kayıtlarını temizler (workspace silindiğinde).
   */
  const clearWorkspaceCache = useCallback((): void => {
    if (!workspaceId || typeof window === 'undefined') return;
    try {
      const prefix = buildKey(workspaceId, '');
      const keysToRemove: string[] = [];
      for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key?.startsWith(prefix)) keysToRemove.push(key);
      }
      keysToRemove.forEach(k => localStorage.removeItem(k));
    } catch { /* sessiz */ }
  }, [workspaceId]);

  return { readCache, writeCache, clearCache, clearWorkspaceCache };
}
