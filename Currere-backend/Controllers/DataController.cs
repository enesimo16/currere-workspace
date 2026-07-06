using System.Security.Claims;
using System.Text;
using Bogus;
using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHuggingFaceService _hfService;

        public DataController(AppDbContext context, IWebHostEnvironment env, IHuggingFaceService hfService)
        {
            _context = context;
            _env = env;
            _hfService = hfService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> IsWorkspaceOwnerAsync(int workspaceId)
        {
            var userId = GetUserId();
            return await _context.Workspaces.AnyAsync(w => w.Id == workspaceId && w.UserId == userId);
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateData(int workspaceId, [FromBody] SyntheticDataRequest request)
        {
            if (!await IsWorkspaceOwnerAsync(workspaceId))
                return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

            try
            {
                string csvData = "";
                
                if (request.Mode == GenerationMode.FastAndFake || request.Mode == (GenerationMode)1)
                {
                    // Standard Mod - Bogus ile
                    csvData = GenerateBogusData(request.Columns, request.RowCount);
                }
                else
                {
                    // AI (Groq/HF) Mod
                    string fullPrompt = string.IsNullOrEmpty(request.Columns) ? request.Prompt : $"Sütunlar: {request.Columns}. {request.Prompt}";
                    csvData = await _hfService.GenerateSyntheticDataAsync(fullPrompt, request.RowCount);
                }

                // Workspace dizinine yaz
                var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var workspaceFolderPath = Path.GetFullPath(Path.Combine(webRootPath, "workspaces", workspaceId.ToString()));
                if (!Directory.Exists(workspaceFolderPath)) Directory.CreateDirectory(workspaceFolderPath);
                
                var safeFileName = string.IsNullOrWhiteSpace(request.FileName) ? "synthetic_data.csv" : request.FileName;
                if (!safeFileName.EndsWith(".csv")) safeFileName += ".csv";
                
                // Benzersiz veya üzerine yaz - UI yeni dosya olarak göstereceği için üzerine yazalım veya benzersiz prefix
                var uniqueFileName = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}_{safeFileName}";
                var physicalPath = Path.Combine(workspaceFolderPath, uniqueFileName);
                
                await System.IO.File.WriteAllTextAsync(physicalPath, csvData, Encoding.UTF8);

                var newDbFile = new WorkspaceFile
                {
                    WorkspaceId = workspaceId,
                    FileName = safeFileName,
                    FilePath = physicalPath,
                    UploadedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(4),
                    IsPermanent = true // Veri setini oluşturduğuna göre kalıcı yapalım
                };
                
                _context.WorkspaceFiles.Add(newDbFile);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Sentetik veri başarıyla üretildi ve çalışma alanınıza kaydedildi.",
                    fileId = newDbFile.Id,
                    fileName = newDbFile.FileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Veri Üretim Hatası: {ex.Message}" });
            }
        }

        private string GenerateBogusData(string columnsString, int rowCount)
        {
            if (string.IsNullOrWhiteSpace(columnsString))
                columnsString = "Id:Number,Name:Name,Email:Email"; // Fallback

            var columns = columnsString.Split(',').Select(c =>
            {
                var parts = c.Split(':');
                return new { Name = parts[0].Trim(), Type = parts.Length > 1 ? parts[1].Trim().ToLower() : "string" };
            }).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columns.Select(c => c.Name)));

            var faker = new Faker();

            for (int i = 0; i < rowCount; i++)
            {
                var row = new List<string>();
                foreach (var col in columns)
                {
                    string val = "";
                    if (col.Type.Contains("name") || col.Type.Contains("isim")) val = faker.Name.FullName();
                    else if (col.Type.Contains("email")) val = faker.Internet.Email();
                    else if (col.Type.Contains("phone") || col.Type.Contains("telefon")) val = faker.Phone.PhoneNumber();
                    else if (col.Type.Contains("date") || col.Type.Contains("tarih")) val = faker.Date.Past().ToString("yyyy-MM-dd");
                    else if (col.Type.Contains("number") || col.Type.Contains("sayı") || col.Type.Contains("yas") || col.Type.Contains("yaş")) val = faker.Random.Number(18, 65).ToString();
                    else if (col.Type.Contains("company") || col.Type.Contains("şirket")) val = faker.Company.CompanyName();
                    else if (col.Type.Contains("address") || col.Type.Contains("adres")) val = faker.Address.FullAddress().Replace(",", " ").Replace("\n", " ");
                    else val = faker.Lorem.Word();

                    // Escape CSV commas
                    if (val.Contains(",")) val = $"\"{val}\"";
                    
                    row.Add(val);
                }
                sb.AppendLine(string.Join(",", row));
            }

            return sb.ToString();
        }
    }
}
