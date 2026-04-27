using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Helpers;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace Currere_backend.Services
{

    // SUAN DOSYA YUKLENIR YUKLENMEZ ANALIZ EDILIYOR
    // ILERIDE BURASINDA DA HANGIRE ILE DOCKERA PASLANARAK ZAMAN KAYBEDİLMEDEN YAPILACAK
    public class FileService : IFileService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env; // Sunucuu fiziki dosyalara erissin diye
        private readonly IDatasetProfilerService _profilerService;

        public FileService(AppDbContext context, IWebHostEnvironment env, IDatasetProfilerService profilerService)
        {
            _context = context;
            _env = env;
            _profilerService = profilerService;
        }

        public async Task<FileUploadResponseDto> UploadFileAsync(int workspaceId, int userId, IFormFile file)
        {
            // workspace == user mı?
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);

            if (workspace == null)
                throw new Exception("Çalışma alanı bulunamadı veya bu alana dosya yükleme yetkiniz yok.");

            // file security, validator
            var validationResult = FileValidator.ValidateFile(file);
            if (!validationResult.IsValid)
                throw new Exception(validationResult.ErrorMessage);

            // PathSanitizer kullanımı
            var sanitizedFileName = PathSanitizer.SanitizeFileName(file.FileName);

            // fiziksel klasor => wwwroot/workspaces/{workspaceId}
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));

            if (!Directory.Exists(workspaceFolderPath))
            {
                Directory.CreateDirectory(workspaceFolderPath);
            }

            // cakismayi önlesin diye dosyanın önüne 8 haneli sayi
            var uniqueFileName = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}_{sanitizedFileName}";
            var fullPhysicalPath = Path.GetFullPath(Path.Combine(workspaceFolderPath, uniqueFileName));

            PathSanitizer.ValidatePathWithinBoundary(fullPhysicalPath, workspaceFolderPath);

            // diske atıp rami koruyoruz
            using (var stream = new FileStream(fullPhysicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4 hours expried
            var workspaceFile = new WorkspaceFile
            {
                WorkspaceId = workspaceId,
                FileName = sanitizedFileName,       // 8 hanesiz original name
                FilePath = fullPhysicalPath,    // fiziksel yol
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(4), // user expire date görecek
                ProfileJson = null // Profil henüz yok, arka planda dolacak!
            };

            // TODO
            // AI BAGLAM
            // Ai ekledigimizde su kısmı aktive edebiliriz;
            // await _aiContextService.ExtractAndSaveMetadataAsync(fullPhysicalPath, file.FileName);
            // bunla birlikte metadata baglam olusturacagiz

            // Dosyayı veritabanına hemen kaydediyoruz ki Id'si oluşsun ve arka plan işçisi dosyayı bulabilsin
            _context.WorkspaceFiles.Add(workspaceFile);
            await _context.SaveChangesAsync();

            // oto db kaydı - json to db (HANGFIRE İLE ARKA PLANA ATILDI)
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension == ".csv" || extension == ".xlsx" || extension == ".json")
            {
                // Kullanıcıyı hiç bekletmeden, işi Hangfire kuyruğuna atıyoruz
                BackgroundJob.Enqueue<IProfileJobService>(job =>
                    job.GenerateAndSaveProfileAsync(workspaceId, uniqueFileName, workspaceFile.Id));
            }

            return new FileUploadResponseDto
            {
                FileId = workspaceFile.Id,
                FileName = workspaceFile.FileName,
                ExpiresAt = workspaceFile.ExpiresAt,
                Message = "Dosya başarıyla yüklendi (Veri profili arka planda çıkarılıyor). Currere kuralları gereği 4 saat boyunca aktif kalacaktır."
            };
        }

        public async Task<List<WorkspaceFileDto>> GetWorkspaceFilesAsync(int workspaceId, int userId)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null)
                throw new Exception("Çalışma alanı bulunamadı veya yetkiniz yok.");

            var files = await _context.WorkspaceFiles
                .Where(f => f.WorkspaceId == workspaceId && f.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(f => f.UploadedAt)
                .Select(f => new WorkspaceFileDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    UploadedAt = f.UploadedAt,
                    ExpiresAt = f.ExpiresAt
                })
                .ToListAsync();

            return files;
        }

        public async Task<string> GetFileContentAsync(int workspaceId, int userId, string fileName)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null)
                throw new Exception("Çalışma alanı bulunamadı veya yetkiniz yok.");

            var sanitizedFileName = PathSanitizer.SanitizeFileName(fileName);

            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.WorkspaceId == workspaceId && f.FileName == sanitizedFileName && f.ExpiresAt > DateTime.UtcNow);

            if (file == null)
                throw new Exception("Dosya bulunamadı veya süresi dolmuş.");

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));
            
            PathSanitizer.ValidatePathWithinBoundary(file.FilePath, workspaceFolderPath);

            if (!System.IO.File.Exists(file.FilePath))
                throw new Exception("Fiziksel dosya sunucuda bulunamadı.");

            return await System.IO.File.ReadAllTextAsync(file.FilePath);
        }

        public async Task<bool> UpdateFileContentAsync(int workspaceId, int userId, string fileName, string content)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null)
                return false;

            var sanitizedFileName = PathSanitizer.SanitizeFileName(fileName);

            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.WorkspaceId == workspaceId && f.FileName == sanitizedFileName && f.ExpiresAt > DateTime.UtcNow);

            if (file == null || !System.IO.File.Exists(file.FilePath))
                return false;

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));
            PathSanitizer.ValidatePathWithinBoundary(file.FilePath, workspaceFolderPath);

            // Güvenli dosya yazma işlemi (File Lock sorunlarını çözmek için FileMode.Create ve FileShare.None)
            byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
            using (var stream = new FileStream(file.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
            }

            return true;
        }

        public async Task<FileUploadResponseDto> CreateFileAsync(int workspaceId, int userId, string fileName)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null)
                throw new Exception("Çalışma alanı bulunamadı veya yetkiniz yok.");

            var sanitizedFileName = PathSanitizer.SanitizeFileName(fileName);

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));

            if (!Directory.Exists(workspaceFolderPath))
            {
                Directory.CreateDirectory(workspaceFolderPath);
            }

            var uniqueFileName = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}_{sanitizedFileName}";
            var fullPhysicalPath = Path.GetFullPath(Path.Combine(workspaceFolderPath, uniqueFileName));

            PathSanitizer.ValidatePathWithinBoundary(fullPhysicalPath, workspaceFolderPath);

            // Create empty file
            await System.IO.File.WriteAllTextAsync(fullPhysicalPath, "");

            var workspaceFile = new WorkspaceFile
            {
                WorkspaceId = workspaceId,
                FileName = sanitizedFileName,
                FilePath = fullPhysicalPath,
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24), // Empty files might last slightly longer, or same 4h. Let's do 4.
                ProfileJson = null
            };
            workspaceFile.ExpiresAt = DateTime.UtcNow.AddHours(4);

            _context.WorkspaceFiles.Add(workspaceFile);
            await _context.SaveChangesAsync();

            return new FileUploadResponseDto
            {
                FileId = workspaceFile.Id,
                FileName = workspaceFile.FileName,
                ExpiresAt = workspaceFile.ExpiresAt,
                Message = "Dosya başarıyla oluşturuldu."
            };
        }
        
        public async Task<bool> DeleteFileAsync(int workspaceId, int userId, string fileName)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null) return false;

            var sanitizedFileName = PathSanitizer.SanitizeFileName(fileName);

            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.WorkspaceId == workspaceId && f.FileName == sanitizedFileName);

            if (file == null) return false;

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));
            PathSanitizer.ValidatePathWithinBoundary(file.FilePath, workspaceFolderPath);

            // Physical delete
            if (System.IO.File.Exists(file.FilePath))
            {
                System.IO.File.Delete(file.FilePath);
            }

            // DB delete
            _context.WorkspaceFiles.Remove(file);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RenameFileAsync(int workspaceId, int userId, string oldFileName, string newFileName)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);
            
            if (workspace == null) return false;

            var sanitizedOldFileName = PathSanitizer.SanitizeFileName(oldFileName);
            var sanitizedNewFileName = PathSanitizer.SanitizeFileName(newFileName);

            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.WorkspaceId == workspaceId && f.FileName == sanitizedOldFileName);

            if (file == null) return false;

            // Check if new name already exists in DB for this workspace
            var exists = await _context.WorkspaceFiles.AnyAsync(f => f.WorkspaceId == workspaceId && f.FileName == sanitizedNewFileName);
            if (exists) throw new Exception("Bu ada sahip bir dosya zaten mevcut.");

            // Physical rename
            var directory = Path.GetDirectoryName(file.FilePath);
            if (directory == null) return false;

            // Generate new physical path (keeping the random prefix if possible, or generating new one)
            // Let's generate a new one to be safe and consistent with Upload logic
            var uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var newPhysicalName = $"{uniquePrefix}_{sanitizedNewFileName}";
            var newFilePath = Path.GetFullPath(Path.Combine(directory, newPhysicalName));

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));

            PathSanitizer.ValidatePathWithinBoundary(file.FilePath, workspaceFolderPath);
            PathSanitizer.ValidatePathWithinBoundary(newFilePath, workspaceFolderPath);

            if (System.IO.File.Exists(file.FilePath))
            {
                System.IO.File.Move(file.FilePath, newFilePath);
            }
            else
            {
                // If physical file missing but DB exists, we should probably still allow rename or handle it
                // For now, let's just create an empty file if it's missing (unlikely case)
                await System.IO.File.WriteAllTextAsync(newFilePath, "");
            }

            // Update DB
            file.FileName = sanitizedNewFileName;
            file.FilePath = newFilePath;
            
            await _context.SaveChangesAsync();

            return true;
        }
    }
}