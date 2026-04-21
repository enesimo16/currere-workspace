using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Currere_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [EnableRateLimiting("AiStrictLimit")] // rate limiting
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly AppDbContext _context;
        private readonly IDatasetProfilerService _profilerService;
        private readonly INotebookConverterService _notebookConverterService;
        private readonly ICodeExecutionService _executionService;
        private readonly IHuggingFaceService _hfService;

        public AiController(IAiService aiService, AppDbContext context, IDatasetProfilerService profilerService, INotebookConverterService notebookConverterService, ICodeExecutionService executionService, IHuggingFaceService hfService)
        {
            _aiService = aiService;
            _context = context;
            _profilerService = profilerService;
            _notebookConverterService = notebookConverterService;
            _executionService = executionService;
            _hfService = hfService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> IsWorkspaceOwnerAsync(int workspaceId)
        {
            var userId = GetUserId();
            return await _context.Workspaces.AnyAsync(w => w.Id == workspaceId && w.UserId == userId);
        }

        [HttpPost("generate-code")]
        public async Task<IActionResult> GenerateCode(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            if (!await IsWorkspaceOwnerAsync(workspaceId)) return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

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
            if (!await IsWorkspaceOwnerAsync(workspaceId)) return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

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
            if (!await IsWorkspaceOwnerAsync(workspaceId)) return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

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
            if (!await IsWorkspaceOwnerAsync(workspaceId)) return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

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

        // converting
        [HttpPost("convert-ipynb-to-py")]
        public async Task<IActionResult> ConvertIpynbToPy(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            if (!await IsWorkspaceOwnerAsync(workspaceId)) return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null)
                return NotFound(new { error = "Dosya bulunamadı." });

            try
            {
                // kodu dosyadan alıyoruz
                string ipynbContent = await System.IO.File.ReadAllTextAsync(file.FilePath);
                string rawPythonCode = await _notebookConverterService.ExtractRawPythonFromNotebookAsync(ipynbContent);

                int maxRetries = 3;
                string currentPyCode = "";
                string lastError = "";
                bool isExecutionSuccessful = false;

                // oto self heal tamir
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    if (attempt == 1)
                    {
                        currentPyCode = await _notebookConverterService.CleanAndOptimizePythonCodeAsync(rawPythonCode);
                    }
                    else
                    {
                        string repairPrompt = $@"Yazdığın kod Docker'da şu hatayı verdi:
HATA LOGU: {lastError}
 
DÜZELTME KURALLARI:
1. Sadece yazım (syntax), girinti (indentation) veya kütüphane hatalarını düzelt.
2. ASLA veri okuma (pd.read_csv vs.) mantığını silme veya yerine sahte işlemler/sleep yazma. Dosya yoksa bile kodu orijinal mantığıyla bırak!
3. Dosya okuma işlemlerini ASLA try-except bloğu içine alma! Bırak kod hata fırlatsın, çevre hatalarını biz sistemde yöneteceğiz.
4. Çıktı SADECE ```python ... ``` bloğu içinde olmalı.
 
HATALARI OLAN KOD:
{currentPyCode}";

                        var rawResponse = await _aiService.ChatAsync(repairPrompt, "Sen katı kurallara uyan bir Python Hata Ayıklayıcısısın.");
                        currentPyCode = NotebookConverterService.StripMarkdownFences(rawResponse);
                    }

                    // docker ile kodun calisip calismadigi testi
                    var executionResult = await _executionService.ExecutePythonCodeAsync(new ExecutionJob { WorkspaceId = workspaceId, Code = currentPyCode });

                    if (executionResult.IsSuccess)
                    {
                        isExecutionSuccessful = true;
                        return Ok(new
                        {
                            message = "Başarılı! Kod hatasız çalıştı ve dönüştürüldü.",
                            code = currentPyCode
                        });
                    }
                    else
                    {
                        lastError = executionResult.Error;

                        // dosya/kütüphame yoksa ai ' ya yollamıyoruz
                        if (executionResult.ErrorType == "FileNotFoundError" ||
                            executionResult.ErrorType == "ModuleNotFoundError")
                        {
                            return Ok(new
                            {
                                message = "Kod başarıyla dönüştürüldü. (Not: Kodun içinde dışarıdan okunan bir dosya/kütüphane olduğu için tam test edilemedi ancak yazım kuralları doğru görünüyor.)",
                                code = currentPyCode,
                                warning = "Test Sırasında Alınan Çevre Hatası: " + executionResult.Error
                            });
                        }

                        // SyntaxError veya IndentationError ise döngü devam eder ve AI düzeltir
                    }
                }

                // 3 denemede de düzelmeyen hata varsa kodu yine de dön, kullanıcı görsün
                if (!isExecutionSuccessful)
                {
                    return Ok(new
                    {
                        message = "Kod dönüştürüldü ancak bazı sözdizimi hataları olabilir. Lütfen kontrol edin.",
                        code = currentPyCode,
                        finalError = lastError
                    });
                }

                return BadRequest();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Dönüşüm Motoru Hatası: {ex.Message}" });
            }
        }

        [HttpPost("generate-synthetic-data")]
        public async Task<IActionResult> GenerateSyntheticData(int workspaceId, [FromBody] GenerateSyntheticDataDto request)
        {
            if (!await IsWorkspaceOwnerAsync(workspaceId)) 
                return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

            try
            {
                var csvData = await _hfService.GenerateSyntheticDataAsync(request.Prompt, request.RowCount);

                return Ok(new
                {
                    message = "Sentetik veri başarıyla üretildi.",
                    csv = csvData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Hugging Face Üretim Hatası: {ex.Message}" });
            }
        }

        [HttpPost("push-to-huggingface")]
        public async Task<IActionResult> PushToHuggingFace(int workspaceId, [FromBody] PushToHubDto request)
        {
            if (!await IsWorkspaceOwnerAsync(workspaceId)) 
                return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

            if (string.IsNullOrEmpty(request.HfToken))
                return BadRequest(new { error = "Hugging Face Access Token bulunamadı. Lütfen ayarlar kısmından giriş yapın." });

            try
            {
                var hubLink = await _hfService.PushToHubAsync(workspaceId, request);

                return Ok(new
                {
                    message = "Model Hub'a başarıyla aktarıldı.",
                    url = hubLink
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Hugging Face Hub Hatası: {ex.Message}" });
            }
        }
    }
}