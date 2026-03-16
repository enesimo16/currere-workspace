using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class KaggleService : IKaggleService
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _env;

        public KaggleService(AppDbContext context, IEncryptionService encryptionService, HttpClient httpClient, IWebHostEnvironment env)
        {
            _context = context;
            _encryptionService = encryptionService;
            _httpClient = httpClient;
            _env = env;
        }

        // Kaggle API'sine yetki ekleme
        private async Task AuthenticateKaggleClientAsync(int userId)
        {
            var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);

            if (integration == null || string.IsNullOrEmpty(integration.KaggleUsername) || string.IsNullOrEmpty(integration.KaggleKey))
                throw new Exception("Kaggle entegrasyonu bulunamadı. Lütfen önce API Key'inizi kaydedin.");

            var decryptedKey = _encryptionService.Decrypt(integration.KaggleKey);
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{integration.KaggleUsername}:{decryptedKey}"));

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Currere-AI-Agent");
        }

        public async Task<string> SearchDatasetsAsync(int userId, string query)
        {
            await AuthenticateKaggleClientAsync(userId);

            // Kaggle Dataset Arama Ucu
            var response = await _httpClient.GetAsync($"https://www.kaggle.com/api/v1/datasets/list?search={query}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Kaggle araması başarısız: {response.ReasonPhrase}");

            return await response.Content.ReadAsStringAsync(); // JSON formatında dataset listesi döner
        }

        public async Task<List<string>> DownloadDatasetAsync(int userId, int workspaceId, string datasetRef)
        {
            await AuthenticateKaggleClientAsync(userId);

            // download istegi, zip olarak 
            var downloadUrl = $"https://www.kaggle.com/api/v1/datasets/download/{datasetRef}";
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Dataset indirilemedi: {response.ReasonPhrase}");

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            if (!Directory.Exists(workspaceFolderPath))
                Directory.CreateDirectory(workspaceFolderPath);

            // Zip dosyasini gecici kaydetme
            var tempZipPath = Path.Combine(workspaceFolderPath, $"kaggle_temp_{Guid.NewGuid()}.zip");

            using (var fs = new FileStream(tempZipPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            // zip threshold ayıklama
            var extractedFiles = new List<string>();
            var extractPath = Path.Combine(workspaceFolderPath, datasetRef.Replace("/", "_")); // Örn: zillow_zecon

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(tempZipPath, extractPath, overwriteFiles: true);

            // Zip içindeki dosyaların yollarını listeye ekleme
            foreach (var file in Directory.GetFiles(extractPath))
            {
                extractedFiles.Add(Path.GetFileName(file));

                // TODO çıkarılan csvleri profilleyeceğiz
            }

            // zip sil
            File.Delete(tempZipPath);

            return extractedFiles; // Çıkarılan dosyaların isimleri
        }
    }
}