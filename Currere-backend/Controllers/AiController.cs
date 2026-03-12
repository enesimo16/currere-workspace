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
            // dosya ve profil kontrolü
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null || string.IsNullOrEmpty(file.ProfileJson))
                return BadRequest(new { error = "Dosya bulunamadı veya profili henüz çıkarılmamış." });

            var fileNameInWorkspace = Path.GetFileName(file.FilePath);

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

        [HttpPost("smart-chat")] // tek adres
        public async Task<IActionResult> SmartChat(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null || string.IsNullOrEmpty(file.ProfileJson))
                return BadRequest(new { error = "Dosya bulunamadı veya profili henüz çıkarılmamış." });

            var fileNameInWorkspace = Path.GetFileName(file.FilePath);

            try
            {
                // kullanıcı istegi renderlama
                var intent = await _aiService.DetermineIntentAsync(request.Prompt);

                if (intent == "SOHBET")
                {
                    string chatContext = $"Kullanıcının şu an üzerinde çalıştığı verinin özeti: {file.ProfileJson}. Kullanıcıya Veri Bilimi bağlamında, Türkçe ve kibarca yanıt ver.";
                    var chatResponse = await _aiService.ChatAsync(request.Prompt, chatContext);

                    return Ok(new { type = "chat", message = chatResponse });
                }
                else
                {
                    var generatedCode = await _aiService.GeneratePythonCodeAsync(request.Prompt, file.ProfileJson, fileNameInWorkspace);

                    return Ok(new { type = "code", code = generatedCode });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI Motoru Hatası: {ex.Message}" });
            }
        }

        [HttpPost("extract-context")]
        public async Task<IActionResult> ExtractHiddenContext(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null || string.IsNullOrEmpty(file.ProfileJson))
                return BadRequest(new { error = "Dosya bulunamadı veya profili henüz çıkarılmamış." });

            // eger daha önce cıkarıldıysa direkt db den cek
            if (!string.IsNullOrEmpty(file.DomainContext))
            {
                return Ok(new
                {
                    message = "Gizli bağlam veritabanından hızlıca getirildi.",
                    insights = file.DomainContext
                });
            }

            string systemPrompt = @"Sen çok tecrübeli bir Veri Analisti ve İş Zekası (Business Intelligence) uzmanısın.
Sana bir veri setinin istatistiksel özetini (JSON formatında) vereceğim. Senden ASLA kod yazmanı İSTEMİYORUM.
Senden beklediğim şey, bu verinin şekline, sütun isimlerine ve değerlerine bakarak şu sorulara profesyonel, Türkçe ve maddeler halinde bir rapor hazırlaman:
1. Sektör ve Hedef: Bu veri hangi sektöre (E-ticaret, Sağlık, İK, Otomotiv vb.) ait olabilir? İçindeki kayıtlar neyi temsil ediyor?
2. Temel İş Kuralları (Business Logic): Sütunlar arasında nasıl mantıksal veya sektörel bir ilişki olabilir? (Örn: 'Yaş arttıkça kıdem artıyor' veya 'Kilometre arttıkça fiyat düşüyor' gibi çıkarımlar yap).
3. Veri Kalitesi Uyarıları: Hangi sütunlarda eksiklik var ve bu durum iş süreçlerini nasıl etkiler?
Raporun okunaklı, profesyonel ve analitik olmalıdır.";

            string userPrompt = $"Veri İstatistikleri Özetim Şudur:\n{file.ProfileJson}";

            try
            {
                var businessContext = await _aiService.ChatAsync(userPrompt, systemPrompt);

                // db saving
                file.DomainContext = businessContext;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Gizli bağlam AI tarafından üretildi ve veritabanına kaydedildi.",
                    insights = businessContext
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI Motoru Hatası: {ex.Message}" });
            }
        }
    }
}