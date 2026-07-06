using Currere_backend.Data;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Services
{
    public class FileCleanupService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileCleanupService> _logger;

        public FileCleanupService(AppDbContext context, IWebHostEnvironment env, ILogger<FileCleanupService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Süresi dolan GEÇİCİ dosyaları temizler.
        /// IsPermanent == true olan kaynak kod dosyaları (.py, .ipynb, .js, .cs vb.)
        /// bu metot tarafından ASLA silinmez.
        /// </summary>
        public async Task CleanupExpiredFilesAsync()
        {
            var now = DateTime.UtcNow;

            // SADECE geçici (IsPermanent == false) ve süresi dolmuş dosyaları getir
            var expiredFiles = await _context.WorkspaceFiles
                .Where(f => !f.IsPermanent && f.ExpiresAt <= now)
                .ToListAsync();

            if (expiredFiles.Count == 0)
            {
                _logger.LogInformation("[FileCleanup] Temizlenecek süresi dolmuş geçici dosya bulunamadı.");
                return;
            }

            _logger.LogInformation("[FileCleanup] {Count} adet süresi dolmuş geçici dosya temizlenecek.", expiredFiles.Count);

            int deletedPhysical = 0;
            int deletedDb = 0;

            foreach (var file in expiredFiles)
            {
                try
                {
                    // Fiziksel dosyayı sil
                    if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                    {
                        File.Delete(file.FilePath);
                        deletedPhysical++;
                        _logger.LogInformation("[FileCleanup] Fiziksel dosya silindi: {Path}", file.FilePath);
                    }

                    _context.WorkspaceFiles.Remove(file);
                    deletedDb++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FileCleanup] Dosya silinirken hata: {FileName} (Id: {Id})", file.FileName, file.Id);
                    // Hata olsa bile diğer dosyaları silmeye devam et
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[FileCleanup] Tamamlandı. Fiziksel: {Physical} silindi, DB: {Db} kayıt kaldırıldı.",
                deletedPhysical, deletedDb);
        }
    }
}