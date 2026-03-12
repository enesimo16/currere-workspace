using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly AppDbContext _context;
        private readonly IDatasetProfilerService _profilerService; 

        public AiController(IAiService aiService, AppDbContext context, IDatasetProfilerService profilerService)
        {
            _aiService = aiService;
            _context = context;
            _profilerService = profilerService;
        }

        [HttpPost("generate-code")]
        public async Task<IActionResult> GenerateCode(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null)
                return NotFound(new { error = "Dosya bulunamadı veya bu çalışma alanına ait değil." });

            var fileNameInWorkspace = Path.GetFileName(file.FilePath);

            if (string.IsNullOrEmpty(file.ProfileJson))
            {
                try
                {
                    // Anında profil
                    var newProfileJson = await _profilerService.ProfileDatasetAsync(workspaceId, fileNameInWorkspace);

                    // Veritabanı
                    file.ProfileJson = newProfileJson;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = $"Sistem veriyi okuyamadı. Dosya formatı bozuk olabilir. Hata: {ex.Message}" });
                }
            }

            try
            {
                var generatedCode = await _aiService.GeneratePythonCodeAsync(request.Prompt, file.ProfileJson, fileNameInWorkspace);

                return Ok(new
                {
                    message = "Yapay zeka kodu başarıyla üretti.",
                    code = generatedCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI Motoru Hatası: {ex.Message}" });
            }
        }

        [HttpPost("auto-preprocess")]
        public async Task<IActionResult> AutoPreprocess(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            // 1. Dosyayı bul ve profilin olduğundan emin ol
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null || string.IsNullOrEmpty(file.ProfileJson))
                return BadRequest(new { error = "Dosya bulunamadı veya profili henüz çıkarılmamış." });

            var fileNameInWorkspace = Path.GetFileName(file.FilePath);

            // 2. İŞTE SİHİR BURADA: GİZLİ MANİFESTO (Prompt Enrichment)
            // Kullanıcının ne yazdığını (request.Prompt) umursamıyoruz bile, kontrol tamamen bizde!
            string magicPrompt = $@"
Sen Currere platformunun 'Otopilot' Veri Bilimi asistanısın. 
Aşağıdaki istatistiklere bakarak veriyi Makine Öğrenmesi modellerine %100 hazır hale getirecek, prodüksiyon kalitesinde SADECE Python kodu üretmektir.

KATI KURALLAR (BUNLARA KESİNLİKLE UY):
1. Veriyi her zaman pd.read_csv('/workspace/{fileNameInWorkspace}') ile gerçek dosyadan oku.
2. Eksik Veri Yönetimi: Eksikliği %50'den fazla olan sütunları sil. Sayısal eksikleri medyan, kategorik eksikleri mod ile doldur.
3. Kategorik Veri Kodlama (Encoding): 'unique_count' değeri çok yüksek olan metin sütunlarında (SMS, uzun yorum vs.) KESİNLİKLE LabelEncoder KULLANMA! Bunlar için 'TfidfVectorizer' kullan. Sadece 2-5 arası eşsiz değeri olan sütunlara LabelEncoder uygula.
4. Sayısal Veri Ölçeklendirme (Scaling): Sayısal sütunlara 'StandardScaler' uygula.
5. Sonuç: Ürettiğin kod markdown formatında (```python ... ```) olmalı ve çalıştığında veriyi tamamen temizlemiş olarak `df_cleaned` adlı bir DataFrame bırakmalıdır. 
Asla açıklama, selamlama veya yorum yapma, sadece çalıştırılabilir kod yaz.";

            try
            {
                // 3. Yapay zekaya kullanıcının zayıf cümlesini değil, bizim devasa manifestomuzu yolluyoruz!
                var generatedCode = await _aiService.GeneratePythonCodeAsync(magicPrompt, file.ProfileJson, fileNameInWorkspace);

                return Ok(new
                {
                    message = "Sihirli Otopilot kodu başarıyla üretti.",
                    code = generatedCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI Motoru Hatası: {ex.Message}" });
            }
        }
    }
}