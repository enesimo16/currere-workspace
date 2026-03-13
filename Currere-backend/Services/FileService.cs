using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Helpers;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;

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

            // fiziksel klasor => wwwroot/workspaces/{workspaceId}
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            if (!Directory.Exists(workspaceFolderPath))
            {
                Directory.CreateDirectory(workspaceFolderPath);
            }

            // cakismayi önlesin diye dosyanın önüne 8 haneli sayi
            var uniqueFileName = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}_{file.FileName}";
            var fullPhysicalPath = Path.Combine(workspaceFolderPath, uniqueFileName);

            // diske atıp rami koruyoruz
            using (var stream = new FileStream(fullPhysicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4 hours expried
            var workspaceFile = new WorkspaceFile
            {
                WorkspaceId = workspaceId,
                FileName = file.FileName,       // 8 hanesiz original name
                FilePath = fullPhysicalPath,    // fiziksel yol
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(4) // user expire date görecek
            };

            // TODO
            // AI BAGLAM
            // Ai ekledigimizde su kısmı aktive edebiliriz;
            // await _aiContextService.ExtractAndSaveMetadataAsync(fullPhysicalPath, file.FileName);
            // bunla birlikte metadata baglam olusturacagiz

            // oto db kaydı - json to db
            // YENİ EKLENEN KISIM: Sadece Veri dosyaları ise profil çıkar!
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension == ".csv" || extension == ".xlsx" || extension == ".json")
            {
                try
                {
                    // Dockerdaki unique dosya adını yolluyoruz
                    var profileJson = await _profilerService.ProfileDatasetAsync(workspaceId, uniqueFileName);
                    workspaceFile.ProfileJson = profileJson;
                }
                catch (Exception ex)
                {
                    // Profilleme hatalı dahi olsa upload olsun
                    Console.WriteLine($"Profil çıkarılamadı: {ex.Message}");
                }
            }

            _context.WorkspaceFiles.Add(workspaceFile);
            await _context.SaveChangesAsync();

            return new FileUploadResponseDto
            {
                FileId = workspaceFile.Id,
                FileName = workspaceFile.FileName,
                ExpiresAt = workspaceFile.ExpiresAt,
                Message = "Dosya başarıyla yüklendi. Currere kuralları gereği 4 saat boyunca aktif kalacaktır."
            };
        }
    }
}