using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Currere_backend.Services
{
    public class GroqAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GroqAiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            // usersecret
            _apiKey = config["AiSettings:GroqApiKey"] ?? throw new Exception("Groq API Anahtarı bulunamadı!");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public Task<string> ChatAsync(string message, string? systemContext = null)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GeneratePythonCodeAsync(string userPrompt, string datasetProfileJson, string fileName)
        {
            var systemMessage = $@"Sen Currere platformunun kıdemli Veri Bilimi asistanısın. 
Kodların, internet bağlantısı olmayan güvenli bir Docker (Sandbox) ortamında çalıştırılacaktır.
İşlenecek asıl veri dosyasının yolu her zaman şudur: /workspace/{fileName}

Aşağıdaki JSON verisi, sadece sütunları, tipleri ve boşlukları tanıman için verilmiş bir ÖZETTİR. 
BU ÖZETİ KESİNLİKLE KODUN İÇİNE KOPYALAMA (HARDCODE YAPMA) VEYA SENTETİK VERİ ÜRETMEYE ÇALIŞMA!
İstatistiksel Özet: {datasetProfileJson}

Görevin: Kullanıcının isteğine göre SADECE Python kodu üretmek. 
Kurallar:
- Veriyi her zaman pd.read_csv('/workspace/{fileName}') ile gerçek dosyadan oku.
- Gerekirse ayraçları tespit etmek için pd.read_csv(..., sep=None, engine='python') kullan.
- Veride olmayan sütun isimlerini ASLA uydurma.
- Kodu markdown formatında (```python ... ```) ver.
- Asla açıklama, selamlama veya yorum yapma. Sadece kod ver.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile", 
                messages = new[]
                {
            new { role = "system", content = systemMessage },
            new { role = "user", content = userPrompt }
        },
                temperature = 0.1
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"AI Motoru Çöktü: {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var aiMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return aiMessage ?? "AI yanıt üretemedi.";
        }
    }
}