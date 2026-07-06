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

            // â”€â”€ BOÅ WORKSPACE KORUMASI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (!Directory.Exists(workspacePath))
            {
                throw new InvalidOperationException("Yedek alÄ±namadÄ±: Ã‡alÄ±ÅŸma alanÄ± klasÃ¶rÃ¼ henÃ¼z oluÅŸturulmamÄ±ÅŸ.");
            }

            var permanentFiles = await _context.WorkspaceFiles
                .Where(f => f.WorkspaceId == workspaceId && f.IsPermanent)
                .ToListAsync();

            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            bool hasCode = workspace != null && !string.IsNullOrEmpty(workspace.CurrentState);

            if (!permanentFiles.Any() && !hasCode)
            {
                throw new InvalidOperationException("Yedek alÄ±namadÄ±: Ã‡alÄ±ÅŸma alanÄ±nda yedeklenecek kalÄ±cÄ± dosya veya kod bulunamadÄ±.");
            }

            _logger.LogInformation("[Snapshot] Yedek oluÅŸturuluyor. WorkspaceId: {WsId}, Dosya sayÄ±sÄ±: {Count}", workspaceId, permanentFiles.Count);

            // â”€â”€ ZIP OLUÅTURMA (SADECE KALICI DOSYALAR) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var snapshotDirectory = Path.Combine(webRootPath, "snapshots", workspaceId.ToString());
            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            var tempZipDir = Path.Combine(snapshotDirectory, $"temp_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempZipDir);

            int fileCount = 0;
            foreach (var file in permanentFiles)
            {
                if (File.Exists(file.FilePath))
                {
                    var dest = Path.Combine(tempZipDir, Path.GetFileName(file.FilePath));
                    File.Copy(file.FilePath, dest, true);
                    fileCount++;
                }
            }

            if (hasCode)
            {
                var codeBackupPath = Path.Combine(tempZipDir, "_currere_code_snapshot.py");
                await File.WriteAllTextAsync(codeBackupPath, workspace!.CurrentState!);
                fileCount++;
            }

            if (fileCount == 0)
            {
                Directory.Delete(tempZipDir, true);
                throw new InvalidOperationException("Yedek alÄ±namadÄ±: Dosyalar fiziksel olarak bulunamadÄ±.");
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var zipFileName = $"snapshot_{timestamp}.zip";
            var zipFilePath = Path.Combine(snapshotDirectory, zipFileName);

            ZipFile.CreateFromDirectory(tempZipDir, zipFilePath, CompressionLevel.Fastest, includeBaseDirectory: false);
            
            // Temizlik
            Directory.Delete(tempZipDir, true);

            // â”€â”€ BOYUT HESAPLAMA â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var zipFileInfo = new FileInfo(zipFilePath);
            long sizeBytes = zipFileInfo.Length;

            if (sizeBytes == 0)
            {
                File.Delete(zipFilePath);
                throw new InvalidOperationException("Yedek alÄ±namadÄ±: OluÅŸturulan zip dosyasÄ± 0 byte. Ä°ÅŸlem iptal edildi.");
            }

            // â”€â”€ KULLANICI ETÄ°KETÄ° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

            _logger.LogInformation("[Snapshot] Yedek oluÅŸturuldu. Id: {Id}, Label: {Label}, Size: {Size}KB, Files: {Count}", 
                snapshot.Id, finalLabel, sizeBytes / 1024, fileCount);

            return snapshot;
        }

        public async Task<bool> RestoreSnapshotAsync(int workspaceId, int snapshotId)
        {
            var snapshot = await _context.WorkspaceSnapshots
                .FirstOrDefaultAsync(s => s.Id == snapshotId && s.WorkspaceId == workspaceId);

            if (snapshot == null)
                throw new Exception("Yedek (Snapshot) bulunamadÄ±.");

            if (!File.Exists(snapshot.ZipFilePath))
                throw new Exception("Yedek dosyasÄ± sunucuda fiziksel olarak bulunamadÄ±.");

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
            var tempRestorePath = Path.Combine(webRootPath, "workspaces", $"{workspaceId}_temp_restore_{Guid.NewGuid():N}");

            // â”€â”€ O-7 Fix: Atomicity â€” Yeni sÄ±ralama â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Eski kod: Workspace silme â†’ temp'ten taÅŸÄ±ma â†’ DB commit. DB hata verirse
            //           dosyalar zaten silinmiÅŸ olabiliyordu â†’ veri kaybÄ± riski.
            //
            // Yeni sÄ±ralama:
            //   1. Zip'i temp klasÃ¶re Ã§Ä±kar ve doÄŸrula (workspace'e dokunma)
            //   2. DB transaction baÅŸlat + workspace metadata gÃ¼ncelle + commit et
            //   3. SADECE commit baÅŸarÄ±lÄ±ysa: workspace dosyalarÄ±nÄ± deÄŸiÅŸtir
            //   4. Herhangi bir adÄ±mda hata: sadece temp klasÃ¶rÃ¼ sil, workspace saÄŸlam kalÄ±r

            // ADIM 1: Zip'i geÃ§ici klasÃ¶re Ã§Ä±kar â€” workspace'e henÃ¼z dokunma
            Directory.CreateDirectory(tempRestorePath);
            try
            {
                ZipFile.ExtractToDirectory(snapshot.ZipFilePath, tempRestorePath, overwriteFiles: true);

                if (Directory.GetFileSystemEntries(tempRestorePath).Length == 0)
                    throw new Exception("HatalÄ± veya boÅŸ yedek! Geri yÃ¼kleme iptal edildi.");
            }
            catch
            {
                // Temp klasÃ¶rÃ¼ temizle, workspace dokunulmadÄ±
                try { Directory.Delete(tempRestorePath, true); } catch { }
                throw;
            }

            // ADIM 2: DB transaction â€” metadata gÃ¼ncelle (dosyalara henÃ¼z dokunma)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var codeBackupPath = Path.Combine(tempRestorePath, "_currere_code_snapshot.py");
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
                _logger.LogInformation("[Snapshot] DB commit baÅŸarÄ±lÄ±. WorkspaceId: {WsId}, SnapshotId: {SnapId} â€” ÅŸimdi dosyalar uygulanÄ±yor.", workspaceId, snapshotId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // DB rollback yapÄ±ldÄ±, workspace dosyalarÄ±na hiÃ§ dokunulmadÄ± â€” gÃ¼vende
                try { Directory.Delete(tempRestorePath, true); } catch { }
                throw new Exception($"Geri yÃ¼kleme baÅŸarÄ±sÄ±z (DB aÅŸamasÄ±): {ex.Message}");
            }

            // ADIM 3: DB commit baÅŸarÄ±lÄ±ysa â€” ÅŸimdi dosyalarÄ± atomik olarak deÄŸiÅŸtir
            // Bu noktada hata olursa workspace bozulabilir ama DB tutarlÄ± kalÄ±r.
            try
            {
                // Workspace'i temizle
                if (Directory.Exists(workspacePath))
                {
                    foreach (var entry in Directory.GetFileSystemEntries(workspacePath))
                    {
                        try
                        {
                            if (Directory.Exists(entry)) Directory.Delete(entry, true);
                            else File.Delete(entry);
                        }
                        catch (IOException ioEx)
                        {
                            throw new Exception($"'{Path.GetFileName(entry)}' dosyasÄ± kullanÄ±mda. Detay: {ioEx.Message}");
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(workspacePath);
                }

                // Temp'ten workspace'e taÅŸÄ±
                foreach (var entry in Directory.GetFileSystemEntries(tempRestorePath))
                {
                    var name = Path.GetFileName(entry);
                    var dest = Path.Combine(workspacePath, name);
                    if (Directory.Exists(entry)) Directory.Move(entry, dest);
                    else File.Move(entry, dest);
                }

                _logger.LogInformation("[Snapshot] Geri yÃ¼kleme BAÅARILI. WorkspaceId: {WsId}, SnapshotId: {SnapId}", workspaceId, snapshotId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Snapshot] DB commit baÅŸarÄ±lÄ± ama DOSYA UYGULAMA baÅŸarÄ±sÄ±z! WorkspaceId: {WsId}", workspaceId);
                throw new Exception($"Geri yÃ¼kleme kÄ±smen baÅŸarÄ±sÄ±z (dosya aÅŸamasÄ±): {ex.Message}");
            }
            finally
            {
                // Temp klasÃ¶rÃ¼ her durumda temizle
                if (Directory.Exists(tempRestorePath))
                    try { Directory.Delete(tempRestorePath, true); } catch { }
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

            // 1. Fiziki dosyayÄ± sil
            if (File.Exists(snapshot.ZipFilePath))
            {
                try { File.Delete(snapshot.ZipFilePath); } catch { /* log and continue */ }
            }

            // 2. DB kaydÄ±nÄ± sil
            _context.WorkspaceSnapshots.Remove(snapshot);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
