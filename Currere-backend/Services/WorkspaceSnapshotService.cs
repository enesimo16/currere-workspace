using System.IO.Compression;
using Currere_backend.Data;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Services
{
    public class WorkspaceSnapshotService : IWorkspaceSnapshotService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<WorkspaceSnapshotService> _logger;

        public WorkspaceSnapshotService(AppDbContext context, IWebHostEnvironment env, ILogger<WorkspaceSnapshotService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public async Task<WorkspaceSnapshot> CreateSnapshotAsync(int workspaceId, string label, string description)
        {
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            // ── BOŞ WORKSPACE KORUMASI ───────────────────────────────────────
            // Klasör yoksa veya boşsa ZIP oluşturma → 0-byte yedek engeli
            if (!Directory.Exists(workspacePath))
            {
                throw new InvalidOperationException(
                    "Yedek alınamadı: Çalışma alanı klasörü henüz oluşturulmamış. Lütfen önce bir dosya yükleyin veya kod yazın.");
            }

            // Veritabanındaki editör kodunu da yedekle
            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            if (workspace != null && !string.IsNullOrEmpty(workspace.CurrentState))
            {
                var codeBackupPath = Path.Combine(workspacePath, "_currere_code_snapshot.py");
                await File.WriteAllTextAsync(codeBackupPath, workspace.CurrentState);
            }

            // Dosya sayısını kontrol et (alt klasörler hariç, sadece dosyalar)
            var workspaceFiles = Directory.GetFiles(workspacePath, "*", SearchOption.AllDirectories);
            if (workspaceFiles.Length == 0)
            {
                throw new InvalidOperationException(
                    "Yedek alınamadı: Çalışma alanında hiç dosya bulunamadı. Boş yedek oluşturmak engellenmiştir.");
            }

            _logger.LogInformation("[Snapshot] Yedek oluşturuluyor. WorkspaceId: {WsId}, Dosya sayısı: {Count}", workspaceId, workspaceFiles.Length);

            // ── ZIP OLUŞTURMA ────────────────────────────────────────────────
            var snapshotDirectory = Path.Combine(webRootPath, "snapshots", workspaceId.ToString());
            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var zipFileName = $"snapshot_{timestamp}.zip";
            var zipFilePath = Path.Combine(snapshotDirectory, zipFileName);

            ZipFile.CreateFromDirectory(workspacePath, zipFilePath, CompressionLevel.Fastest, includeBaseDirectory: false);

            // ── BOYUT VE DOSYA SAYISI HESAPLAMA ──────────────────────────────
            var zipFileInfo = new FileInfo(zipFilePath);
            long sizeBytes = zipFileInfo.Length;
            int fileCount = workspaceFiles.Length;

            // Son doğrulama: Zip 0 byte ise sil ve hata fırlat
            if (sizeBytes == 0)
            {
                File.Delete(zipFilePath);
                throw new InvalidOperationException("Yedek alınamadı: Oluşturulan zip dosyası 0 byte. İşlem iptal edildi.");
            }

            // ── KULLANICI ETİKETİ ────────────────────────────────────────────
            var finalLabel = !string.IsNullOrWhiteSpace(label) 
                ? label.Trim() 
                : $"Otomatik Yedek - {DateTime.Now:dd.MM.yyyy HH:mm}";

            var snapshot = new WorkspaceSnapshot
            {
                WorkspaceId = workspaceId,
                Label = finalLabel,
                Description = string.IsNullOrWhiteSpace(description) ? finalLabel : description.Trim(),
                ZipFilePath = zipFilePath,
                SizeBytes = sizeBytes,
                FileCount = fileCount,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkspaceSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Snapshot] Yedek oluşturuldu. Id: {Id}, Label: {Label}, Size: {Size}KB, Files: {Count}", 
                snapshot.Id, finalLabel, sizeBytes / 1024, fileCount);

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

            // ── PRE-RESTORE GÜVENLİK YEDEĞİ ────────────────────────────────
            // Geri yükleme başarısız olursa bu yedekten dönülecek
            string? preRestoreZipPath = null;
            if (Directory.Exists(workspacePath) && Directory.GetFileSystemEntries(workspacePath).Length > 0)
            {
                var safetyDir = Path.Combine(webRootPath, "snapshots", workspaceId.ToString());
                if (!Directory.Exists(safetyDir))
                    Directory.CreateDirectory(safetyDir);

                preRestoreZipPath = Path.Combine(safetyDir, $"pre_restore_{DateTime.UtcNow.Ticks}.zip");
                
                try
                {
                    ZipFile.CreateFromDirectory(workspacePath, preRestoreZipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
                    _logger.LogInformation("[Snapshot] Pre-restore güvenlik yedeği alındı: {Path}", preRestoreZipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Snapshot] Pre-restore güvenlik yedeği alınamadı, devam ediliyor...");
                    preRestoreZipPath = null; // Güvenlik yedeği alınamazsa bile devam et
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Seçilen yedeği geçici klasöre aç ve doğrula
                if (Directory.Exists(tempRestorePath)) Directory.Delete(tempRestorePath, true);
                Directory.CreateDirectory(tempRestorePath);

                ZipFile.ExtractToDirectory(snapshot.ZipFilePath, tempRestorePath, overwriteFiles: true);

                if (Directory.GetFileSystemEntries(tempRestorePath).Length == 0)
                {
                    Directory.Delete(tempRestorePath, true);
                    throw new Exception("Hatalı veya boş yedek! Sistem dosyaları korunarak geri yükleme iptal edildi.");
                }

                // 2. Mevcut workspace'i temizle (dosyaları tek tek sil — klasör yapısını koru)
                if (Directory.Exists(workspacePath))
                {
                    foreach (var entry in Directory.GetFileSystemEntries(workspacePath))
                    {
                        try
                        {
                            if (Directory.Exists(entry))
                                Directory.Delete(entry, true);
                            else
                                File.Delete(entry);
                        }
                        catch (IOException ioEx)
                        {
                            throw new Exception($"'{Path.GetFileName(entry)}' dosyası bir işlem tarafından kullanılıyor olabilir. Detay: {ioEx.Message}");
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(workspacePath);
                }

                // 3. Geçici klasördeki dosyaları workspace'e taşı
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

                // Başarılı — geçici klasörü ve güvenlik yedeğini temizle
                if (Directory.Exists(tempRestorePath)) Directory.Delete(tempRestorePath, true);
                // Pre-restore yedeğini başarılı geri yüklemeden sonra sil (artık gereksiz)
                if (!string.IsNullOrEmpty(preRestoreZipPath) && File.Exists(preRestoreZipPath))
                    File.Delete(preRestoreZipPath);

                _logger.LogInformation("[Snapshot] Geri yükleme BAŞARILI. WorkspaceId: {WsId}, SnapshotId: {SnapId}", workspaceId, snapshotId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // Geçici klasörü temizle
                if (Directory.Exists(tempRestorePath))
                {
                    try { Directory.Delete(tempRestorePath, true); } catch { }
                }

                // ── TRANSACTION ROLLBACK: Güvenlik yedeğinden geri dön ───────
                if (!string.IsNullOrEmpty(preRestoreZipPath) && File.Exists(preRestoreZipPath))
                {
                    _logger.LogWarning("[Snapshot] Geri yükleme BAŞARISIZ, güvenlik yedeğinden dönülüyor...");
                    try
                    {
                        // Mevcut (bozulmuş olabilecek) workspace'i temizle
                        if (Directory.Exists(workspacePath))
                        {
                            foreach (var entry in Directory.GetFileSystemEntries(workspacePath))
                            {
                                try
                                {
                                    if (Directory.Exists(entry)) Directory.Delete(entry, true);
                                    else File.Delete(entry);
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(workspacePath);
                        }

                        // Güvenlik yedeğini aç
                        ZipFile.ExtractToDirectory(preRestoreZipPath, workspacePath, overwriteFiles: true);
                        _logger.LogInformation("[Snapshot] Güvenlik yedeğinden GERİ DÖNÜLDÜ.");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "[Snapshot] Güvenlik yedeğinden geri dönüş de başarısız!");
                    }
                    finally
                    {
                        // Güvenlik yedeğini temizle
                        try { File.Delete(preRestoreZipPath); } catch { }
                    }
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