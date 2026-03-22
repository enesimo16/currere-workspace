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
                throw new Exception("Çalışma alanı bulunamadı. Yedeklenecek dosya yok.");

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

            // workspace sil
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, true);
            }
            Directory.CreateDirectory(workspacePath);

            // yedeği workspaceye
            ZipFile.ExtractToDirectory(snapshot.ZipFilePath, workspacePath, overwriteFiles: true);

            return true;
        }

        public async Task<List<WorkspaceSnapshot>> GetSnapshotsAsync(int workspaceId)
        {
            return await _context.WorkspaceSnapshots
                .Where(s => s.WorkspaceId == workspaceId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
    }
}