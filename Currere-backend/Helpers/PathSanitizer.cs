using System;
using System.IO;

namespace Currere_backend.Helpers
{
    public static class PathSanitizer
    {
        /// <summary>
        /// Dosya adını güvenli hale getirir.
        /// Traversal girişimi (.., /, \) tespit edilirse ANINDA reddeder.
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Dosya adı boş olamaz.");

            // ═══ SALDIRI TESPİTİ — traversal girişimlerini anında reddet ═══
            if (fileName.Contains("..") ||
                fileName.Contains('/') ||
                fileName.Contains('\\') ||
                fileName.Contains('%'))
            {
                throw new ArgumentException(
                    "Geçersiz dosya adı: Dizin atlama girişimi tespit edildi.");
            }

            // Geçersiz dosya adı karakterlerini temizle
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..")
                throw new ArgumentException("Geçersiz dosya adı.");

            return fileName;
        }

        /// <summary>
        /// Verilen yolun, belirlenen sınır dizininin içinde olduğunu doğrular.
        /// Her iki yolu da Path.GetFullPath ile normalize eder.
        /// Sınır dışındaysa UnauthorizedAccessException fırlatır.
        /// </summary>
        public static void ValidatePathWithinBoundary(string fullPath, string boundaryPath)
        {
            var normalizedFull = Path.GetFullPath(fullPath);
            var normalizedBoundary = Path.GetFullPath(boundaryPath);

            // Sınır yolunun sonuna separator ekle — "workspaces/4" ile "workspaces/40" karışmasın
            if (!normalizedBoundary.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                normalizedBoundary += Path.DirectorySeparatorChar;
            }

            if (!normalizedFull.StartsWith(normalizedBoundary, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    "Erişim reddedildi: Yol workspace sınırları dışında.");
            }
        }
    }
}
