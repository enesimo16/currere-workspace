using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Currere_backend.Data;
using Currere_backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class HuggingFaceService : IHuggingFaceService
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _env;
        private readonly IAiService _aiService;
        private readonly IConfiguration _configuration;

        public HuggingFaceService(
            AppDbContext context,
            IEncryptionService encryptionService,
            HttpClient httpClient,
            IWebHostEnvironment env,
            IAiService aiService,
            IConfiguration configuration)
        {
            _context = context;
            _encryptionService = encryptionService;
            _httpClient = httpClient;
            _env = env;
            _aiService = aiService;
            _configuration = configuration;
        }

        private async Task<string> GetHuggingFaceTokenAsync(int userId)
        {
            var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);
            if (integration == null || string.IsNullOrEmpty(integration.HuggingFaceToken))
                throw new Exception("HuggingFace Token bulunamadı. Lütfen entegrasyon ayarlarından kaydedin.");

            return _encryptionService.Decrypt(integration.HuggingFaceToken);
        }

        // user kullanici adi
        private async Task<string> GetHuggingFaceUsernameAsync(string token)
        {
            var response = await _httpClient.GetAsync("https://huggingface.co/api/whoami-v2");
            if (!response.IsSuccessStatusCode)
                throw new Exception("Hugging Face API yetkilendirme hatası. Token geçersiz olabilir.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("name").GetString() ?? throw new Exception("Kullanıcı adı okunamadı.");
        }

        public async Task<string> DeployModelToSpaceAsync(int userId, int workspaceId, string spaceName, string modelFileName)
        {
            // authtentication
            var token = await GetHuggingFaceTokenAsync(userId);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var hfUsername = await GetHuggingFaceUsernameAsync(token);
            var repoId = $"{hfUsername}/{spaceName}"; // Örn: enesyel/Titanic-Predictor

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
            var modelPath = Path.Combine(workspacePath, modelFileName);

            if (!File.Exists(modelPath))
                throw new Exception($"Belirtilen model dosyası ({modelFileName}) çalışma alanında bulunamadı.");

            // AI'a app.py ve requirements.txt Yazdırma
            var systemPrompt = "Sen kıdemli bir MLOps mühendisisin. Görevin bir makine öğrenmesi modeli için Gradio web arayüzü (app.py) yazmak.";
            var userPrompt = $"Çalışma dizininde '{modelFileName}' adında bir model var. Bu modeli yükleyip temel bir tahmin arayüzü sunan 'app.py' kodu yaz. SADECE Python kodunu ver.";

            var appPyCode = await _aiService.ChatAsync(userPrompt, systemPrompt);
            appPyCode = appPyCode.Replace("```python", "").Replace("```", "").Trim();
            var reqTxtCode = "gradio\nscikit-learn\npandas\nnumpy\njoblib";

            // repo oluşturma
            var repoRequest = new { type = "space", name = spaceName, sdk = "gradio", private_repo = false };
            var content = new StringContent(JsonSerializer.Serialize(repoRequest), Encoding.UTF8, "application/json");

            var createResponse = await _httpClient.PostAsync("https://huggingface.co/api/repos/create", content);
            if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                throw new Exception($"Space oluşturulamadı: {error}");
            }

            // hf'ye commit atma
            // Model dosyasını Base64 formatına çeviriyoruz ki bozulmadan gitsin
            var modelBytes = await File.ReadAllBytesAsync(modelPath);
            var modelBase64 = Convert.ToBase64String(modelBytes);

            // HuggingFace Commit API Formatı
            var commitPayload = new
            {
                commit_message = "Otonom Currere AI tarafından deploy edildi",
                operations = new[]
                {
                    // Arayüz Dosyası (app.py)
                    new { operation = "add", path = "app.py", content = appPyCode, encoding = "utf-8" },
                    
                    // Kütüphane Dosyası (requirements.txt)
                    new { operation = "add", path = "requirements.txt", content = reqTxtCode, encoding = "utf-8" },
                    
                    // Eğitilmiş Makine Öğrenmesi Modeli (.pkl, .h5 vs)
                    new { operation = "add", path = modelFileName, content = modelBase64, encoding = "base64" }
                }
            };

            var commitContent = new StringContent(JsonSerializer.Serialize(commitPayload), Encoding.UTF8, "application/json");
            var commitResponse = await _httpClient.PostAsync($"https://huggingface.co/api/spaces/{repoId}/commit/main", commitContent);

            if (!commitResponse.IsSuccessStatusCode)
            {
                var error = await commitResponse.Content.ReadAsStringAsync();
                throw new Exception($"Dosyalar HuggingFace'e aktarılamadı: {error}");
            }

            // Space ayağa kalkma süresi vardır
            // dolayısıyla sadece linkini dönüyoruz
            return $"https://huggingface.co/spaces/{repoId}";
        }

        public async Task<string> GenerateSyntheticDataAsync(string prompt, int rowCount)
        {
            var apiKey = _configuration["HuggingFace:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("Hugging Face API Key bulunamadı. Lütfen appsettings.json'ı kontrol edin.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var systemInstruction = $@"Sen bir sentetik veri üreticisisin. Kullanıcının verdiği konuya göre tam olarak {rowCount} satırlık bir veri seti üret. 
SADECE virgülle ayrılmış geçerli bir CSV metni döndür. 
ASLA markdown (```csv) kullanma, ASLA açıklama, merhaba veya not yazma. 
İlk satır sütun başlıkları olsun, ardından veriler gelsin.";

            var requestBody = new
            {
                inputs = $"{systemInstruction}\n\nKullanıcı İsteği: {prompt}",
                parameters = new
                {
                    max_new_tokens = 2048,
                    temperature = 0.7,
                    return_full_text = false
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api-inference.huggingface.co/models/meta-llama/Meta-Llama-3-8B-Instruct", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Hugging Face Inference API Hatası: {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            // Inference API genellikle liste döner: [ { "generated_text": "..." } ]
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var generatedText = doc.RootElement[0].GetProperty("generated_text").GetString();
                return generatedText?.Trim() ?? string.Empty;
            }

            return jsonResponse;
        }

        public async Task<string> PushToHubAsync(int workspaceId, PushToHubDto dto)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dto.HfToken);

            // 1. Repo ID Hazırla
            var finalRepoId = dto.RepoName;
            if (!dto.RepoName.Contains("/"))
            {
                var username = await GetHuggingFaceUsernameAsync(dto.HfToken);
                finalRepoId = $"{username}/{dto.RepoName}";
            }

            // 2. Repo oluşturmayı dene (Varsa hata verir, yutuyoruz)
            var repoRequest = new { type = "model", name = finalRepoId.Split('/').Last(), private_repo = dto.IsPrivate };
            var createContent = new StringContent(JsonSerializer.Serialize(repoRequest), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync("https://huggingface.co/api/repos/create", createContent);

            // 3. Dosya yolunu oluştur
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var filePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString(), dto.FileName);

            if (!File.Exists(filePath))
                throw new Exception($"Dosya bulunamadı: {dto.FileName}");

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var extension = Path.GetExtension(dto.FileName).ToLower();
            
            bool isBinary = new[] { ".pkl", ".h5", ".bin", ".pt", ".onnx", ".joblib", ".model" }.Contains(extension);
            var contentStr = isBinary ? Convert.ToBase64String(fileBytes) : Encoding.UTF8.GetString(fileBytes);
            var encoding = isBinary ? "base64" : "utf-8";

            // 4. Commit At (Push)
            var commitPayload = new
            {
                commit_message = dto.CommitMessage,
                operations = new[]
                {
                    new { operation = "add", path = dto.FileName, content = contentStr, encoding = encoding }
                }
            };

            var commitContent = new StringContent(JsonSerializer.Serialize(commitPayload), Encoding.UTF8, "application/json");
            var commitResponse = await _httpClient.PostAsync($"https://huggingface.co/api/models/{finalRepoId}/commit/main", commitContent);

            if (!commitResponse.IsSuccessStatusCode)
            {
                var error = await commitResponse.Content.ReadAsStringAsync();
                throw new Exception($"Hugging Face Hub Hatası: {error}");
            }

            return $"https://huggingface.co/models/{finalRepoId}";
        }
    }
}