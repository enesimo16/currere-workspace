using System.IO.Compression;
using Currere_backend.Data;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class WorkspaceSnapshotService : IWorkspaceSnapshotService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public WorkspaceSnapshotService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<WorkspaceSnapshot> CreateSnapshotAsync(int workspaceId, string description)
        {
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            // Yedeğin tutulacağı klasör
            // aynı klasörde tutmuyoruz
            var snapshotDirectory = Path.Combine(webRootPath, "snapshots", workspaceId.ToString());

            if (!Directory.Exists(workspacePath))
                Directory.CreateDirectory(workspacePath);

            // KRITIK: Veritabanındaki editör kodunu da yedekle (Eğer klasör boşsa yedek boş kalmasın)
            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            if (workspace != null && !string.IsNullOrEmpty(workspace.CurrentState))
            {
                var codeBackupPath = Path.Combine(workspacePath, "_currere_code_snapshot.py");
                await File.WriteAllTextAsync(codeBackupPath, workspace.CurrentState);
            }

            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var zipFileName = $"snapshot_{timestamp}.zip";
            var zipFilePath = Path.Combine(snapshotDirectory, zipFileName);

            // zipleme
            ZipFile.CreateFromDirectory(workspacePath, zipFilePath, CompressionLevel.Fastest, includeBaseDirectory: false);

            var snapshot = new WorkspaceSnapshot
            {
                WorkspaceId = workspaceId,
                Description = string.IsNullOrWhiteSpace(description) ? "Otomatik Yedek" : description,
                ZipFilePath = zipFilePath,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkspaceSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            return snapshot;
        }

        public async Task<bool> RestoreSnapshotAsync(int workspaceId, int snapshotId)
        {
            var snapshot = await _context.WorkspaceSnapshots
                .FirstOrDefaultAsync(s => s.Id == snapshotId && s.WorkspaceId == workspaceId);

            if (snapshot == null)
                throw new Exception("Yedek (Snapshot) bulunamadı.");

            if (!File.Exists(snapshot.ZipFilePath))
                throw new Exception("Yedek dosyası sunucuda fiziksel olarak bulunamadı.");

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
            var tempRestorePath = Path.Combine(webRootPath, "workspaces", $"{workspaceId}_temp_restore");
            var backupPath = Path.Combine(webRootPath, "workspaces", $"{workspaceId}_backup_{DateTime.UtcNow.Ticks}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Önce geçici klasöre aç ve doğrula
                if (Directory.Exists(tempRestorePath)) Directory.Delete(tempRestorePath, true);
                Directory.CreateDirectory(tempRestorePath);

                ZipFile.ExtractToDirectory(snapshot.ZipFilePath, tempRestorePath, overwriteFiles: true);

                if (Directory.GetFileSystemEntries(tempRestorePath).Length == 0)
                {
                    Directory.Delete(tempRestorePath, true);
                    throw new Exception("Hatalı veya boş yedek! Sistem dosyaları korunarak geri yükleme iptal edildi.");
                }

                // 2. Mevcut çalışma alanını yedekle (Daha dirençli yöntem: öğeleri tek tek taşı)
                if (Directory.Exists(workspacePath))
                {
                    Directory.CreateDirectory(backupPath);
                    foreach (var entry in Directory.GetFileSystemEntries(workspacePath))
                    {
                        var name = Path.GetFileName(entry);
                        var dest = Path.Combine(backupPath, name);
                        try
                        {
                            if (Directory.Exists(entry)) Directory.Move(entry, dest);
                            else File.Move(entry, dest);
                        }
                        catch (IOException ioEx)
                        {
                            throw new Exception($"'{name}' dosyası bir işlem tarafından kullanılıyor olabilir. Lütfen açık terminal veya dosyaları kapatıp tekrar deneyin. Detay: {ioEx.Message}");
                        }
                    }
                    // Ana klasörü silmeye çalışma, sadece içini boşaltmış olduk (Windows kilitlerini aşmak için)
                }

                // 3. Geçici klasördeki yeni dosyaları asıl çalışma alanına aktar
                foreach (var entry in Directory.GetFileSystemEntries(tempRestorePath))
                {
                    var name = Path.GetFileName(entry);
                    var dest = Path.Combine(workspacePath, name);
                    if (Directory.Exists(entry)) Directory.Move(entry, dest);
                    else File.Move(entry, dest);
                }

                // 4. Veritabanındaki kodu zipten çıkan kodla senkronize et
                var codeBackupPath = Path.Combine(workspacePath, "_currere_code_snapshot.py");
                if (File.Exists(codeBackupPath))
                {
                    var restoredCode = await File.ReadAllTextAsync(codeBackupPath);
                    var workspaceToUpdate = await _context.Workspaces.FindAsync(workspaceId);
                    if (workspaceToUpdate != null)
                    {
                        workspaceToUpdate.CurrentState = restoredCode;
                        _context.Workspaces.Update(workspaceToUpdate);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                // Temizlik
                if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);
                if (Directory.Exists(tempRestorePath)) Directory.Delete(tempRestorePath, true);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                if (Directory.Exists(tempRestorePath)) Directory.Delete(tempRestorePath, true);
                
                // Hata durumunda yedekleri geri yüklemeye çalış
                if (Directory.Exists(backupPath) && Directory.Exists(workspacePath))
                {
                    foreach (var entry in Directory.GetFileSystemEntries(backupPath))
                    {
                        var name = Path.GetFileName(entry);
                        var dest = Path.Combine(workspacePath, name);
                        if (Directory.Exists(entry)) Directory.Move(entry, dest);
                        else File.Move(entry, dest);
                    }
                    Directory.Delete(backupPath, true);
                }

                throw new Exception($"Geri yükleme başarısız: {ex.Message}");
            }
        }

        public async Task<List<WorkspaceSnapshot>> GetSnapshotsAsync(int workspaceId)
        {
            return await _context.WorkspaceSnapshots
                .Where(s => s.WorkspaceId == workspaceId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteSnapshotAsync(int workspaceId, int snapshotId)
        {
            var snapshot = await _context.WorkspaceSnapshots
                .FirstOrDefaultAsync(s => s.Id == snapshotId && s.WorkspaceId == workspaceId);

            if (snapshot == null) return false;

            // 1. Fiziki dosyayı sil
            if (File.Exists(snapshot.ZipFilePath))
            {
                try { File.Delete(snapshot.ZipFilePath); } catch { /* log and continue */ }
            }

            // 2. DB kaydını sil
            _context.WorkspaceSnapshots.Remove(snapshot);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}