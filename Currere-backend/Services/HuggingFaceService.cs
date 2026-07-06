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

        // K-1 Fix: Global header mutasyonu yok. Her istek kendi Authorization header'lı mesajını üretir.
        private async Task<string> GetHuggingFaceUsernameAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/api/whoami-v2");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Hugging Face API yetkilendirme hatası. Token geçersiz olabilir.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("name").GetString() ?? throw new Exception("Kullanıcı adı okunamadı.");
        }

        public async Task<string> DeployModelToSpaceAsync(int userId, int workspaceId, string spaceName, string modelFileName)
        {
            // K-1 Fix: Global header mutasyonu kaldırıldı — per-request header kullanılıyor.
            var token = await GetHuggingFaceTokenAsync(userId);
            var hfUsername = await GetHuggingFaceUsernameAsync(token);
            var repoId = $"{hfUsername}/{spaceName}";

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
            var modelPath = Path.Combine(workspacePath, modelFileName);

            if (!File.Exists(modelPath))
                throw new Exception($"Belirtilen model dosyası ({modelFileName}) çalışma alanında bulunamadı.");

            var systemPrompt = "Sen kıdemli bir MLOps mühendisisin. Görevin bir makine öğrenmesi modeli için Gradio web arayüzü (app.py) yazmak.";
            var userPrompt = $"Çalışma dizininde '{modelFileName}' adında bir model var. Bu modeli yükleyip temel bir tahmin arayüzü sunan 'app.py' kodu yaz. SADECE Python kodunu ver.";

            var appPyCode = await _aiService.ChatAsync(userPrompt, systemPrompt);
            appPyCode = appPyCode.Replace("```python", "").Replace("```", "").Trim();
            var reqTxtCode = "gradio\nscikit-learn\npandas\nnumpy\njoblib";

            var repoRequest = new { type = "space", name = spaceName, sdk = "gradio", private_repo = false };
            using var createReq = new HttpRequestMessage(HttpMethod.Post, "https://huggingface.co/api/repos/create");
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createReq.Content = new StringContent(JsonSerializer.Serialize(repoRequest), Encoding.UTF8, "application/json");
            var createResponse = await _httpClient.SendAsync(createReq);
            if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                throw new Exception($"Space oluşturulamadı: {error}");
            }

            // hf'ye commit atma — app.py ve requirements.txt (metin, küçük)
            var modelBytes = await File.ReadAllBytesAsync(modelPath);
            var modelBase64 = Convert.ToBase64String(modelBytes);

            var commitPayload = new
            {
                commit_message = "Otonom Currere AI tarafından deploy edildi",
                operations = new[]
                {
                    new { operation = "add", path = "app.py", content = appPyCode, encoding = "utf-8" },
                    new { operation = "add", path = "requirements.txt", content = reqTxtCode, encoding = "utf-8" },
                    new { operation = "add", path = modelFileName, content = modelBase64, encoding = "base64" }
                }
            };

            using var commitReq = new HttpRequestMessage(HttpMethod.Post, $"https://huggingface.co/api/spaces/{repoId}/commit/main");
            commitReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            commitReq.Content = new StringContent(JsonSerializer.Serialize(commitPayload), Encoding.UTF8, "application/json");
            var commitResponse = await _httpClient.SendAsync(commitReq);

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

        public async Task<string> PushToHubAsync(int userId, int workspaceId, PushToHubDto dto)
        {
            // K-1 + K-2 Fix:
            // - Global DefaultRequestHeaders ve Timeout mutasyonları KALDIRILDI.
            // - Büyük binary dosyalar için ReadAllBytesAsync yerine streaming upload (FileStream) kullanılıyor.
            // - Her HTTP çağrısı kendi HttpRequestMessage'unu oluşturuyor.
            // - Timeout için CancellationTokenSource kullanılıyor (global Timeout mutasyonu yok).
            var token = string.IsNullOrEmpty(dto.HfToken) ? await GetHuggingFaceTokenAsync(userId) : dto.HfToken;
            using var cts = new CancellationTokenSource(TimeSpan.FromHours(2));

            var finalRepoId = dto.RepoName;
            if (!dto.RepoName.Contains("/"))
            {
                var username = await GetHuggingFaceUsernameAsync(token);
                finalRepoId = $"{username}/{dto.RepoName}";
            }

            var repoRequest = new { type = "model", name = finalRepoId.Split('/').Last(), private_repo = dto.IsPrivate };
            using var createRepoReq = new HttpRequestMessage(HttpMethod.Post, "https://huggingface.co/api/repos/create");
            createRepoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createRepoReq.Content = new StringContent(JsonSerializer.Serialize(repoRequest), Encoding.UTF8, "application/json");
            await _httpClient.SendAsync(createRepoReq, cts.Token); // Hata yutulur (repo varsa 409)

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var targetPath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString(), dto.FileName);

            if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
                throw new Exception($"Belirtilen hedef (Dosya veya Klasör) bulunamadı: {dto.FileName}");

            // K-2 Fix: Büyük binary dosyalar için ReadAllBytesAsync KALDIRILDI.
            // HuggingFace'in doğrudan yükleme endpoint'i (upload/main/{path}) kullanılıyor.
            // FileStream → StreamContent → HTTP PUT — dosya hiç RAM'e alınmıyor.
            async Task UploadFileStreamedAsync(string localFilePath, string pathInRepo)
            {
                using var fileStream = File.OpenRead(localFilePath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                using var uploadReq = new HttpRequestMessage(
                    HttpMethod.Put,
                    $"https://huggingface.co/api/models/{finalRepoId}/upload/main/{Uri.EscapeDataString(pathInRepo)}");
                uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                uploadReq.Content = streamContent;

                var uploadResp = await _httpClient.SendAsync(uploadReq, cts.Token);
                if (!uploadResp.IsSuccessStatusCode)
                {
                    var err = await uploadResp.Content.ReadAsStringAsync(cts.Token);
                    throw new Exception($"Dosya yükleme hatası ({pathInRepo}): {err}");
                }
            }

            if (Directory.Exists(targetPath))
            {
                var files = Directory.GetFiles(targetPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(targetPath, file).Replace("\\", "/");
                    await UploadFileStreamedAsync(file, $"{dto.FileName}/{relativePath}");
                }
            }
            else
            {
                await UploadFileStreamedAsync(targetPath, dto.FileName);
            }

            return $"https://huggingface.co/models/{finalRepoId}";
        }
    }
}