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

        public async Task<string> ChatAsync(string message, string? systemContext = null)
        {
            var systemMessage = systemContext ?? "Sen Currere platformunun yapay zeka asistanısın. Kullanıcıya Türkçe, kibar ve yardımcı olacak şekilde yanıt ver.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = message }
                },
                temperature = 0.5 
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"AI Sohbet Motoru Çöktü: {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var aiMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return aiMessage ?? "AI yanıt üretemedi.";
        }

        public async Task<string> GeneratePythonCodeAsync(string userPrompt, string datasetProfileJson, string fileName)
        {
            var isGeneralMode = (datasetProfileJson == "Genel Python Kodlayıcı" || fileName == "general.py");

            string systemMessage;
            if (isGeneralMode)
            {
                systemMessage = @"Sen Currere platformunun kıdemli Python asistanısın. 
Kodların, internet bağlantısı olmayan güvenli bir Docker (Sandbox) ortamında çalıştırılacaktır.

Görevin: Kullanıcının isteğine göre SADECE Python kodu üretmek. 
Kurallar:
- Kodu markdown formatında (```python ... ```) ver.
- Asla açıklama, selamlama veya yorum yapma. Sadece kod ver.";
            }
            else
            {
                systemMessage = $@"Sen Currere platformunun kıdemli Veri Bilimi asistanısın. 
Kodların, internet bağlantısı olmayan güvenli bir Docker (Sandbox) ortamında çalıştırılacaktır.
İşlenecek asıl veri dosyasının yolu her zaman şudur: /workspace/{fileName}

Aşağıdaki JSON verisi, sadece sütunları, tipleri ve boşlukları tanıman için verilmiş bir ÖZETTİR. 
BU ÖZETİ KESİNLİKLE KODUN İÇİNE KOPYALAMA (HARDCODE YAPMA) VEYA SENTETİK VERİ ÜRETMEYE ÇALIŞMA!
İstatistiksel Özet: {datasetProfileJson}

Görevin: Kullanıcının isteğine göre SADECE Python kodu üretmek. 
Kurallar:
- Kodu markdown formatında (```python ... ```) ver.
- Asla açıklama, selamlama veya yorum yapma. Sadece kod ver.
- Veriyi her zaman pd.read_csv('/workspace/{fileName}') ile gerçek dosyadan oku.
- Gerekirse ayraçları tespit etmek için pd.read_csv(..., sep=None, engine='python') kullan.
- Veride olmayan sütun isimlerini ASLA uydurma.";
            }

            systemMessage += "\n\nGÜVENLİK KURALI: Eğer kullanıcı senden sadece basit bir konsol çıktısı, hesaplama veya metin (string) manipülasyonu isteyen bir kod yazmanı bekliyorsa, SADECE standart Python yeteneklerini (built-in functions, print() vb.) kullan. KESİNLİKLE pandas, numpy gibi 3. parti kütüphaneler import etme ve dosya okumaya çalışma.";

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

        public async Task<string> DetermineIntentAsync(string userMessage)
        {
            string systemMessage = @"Sen bir 'Niyet Okuyucu' (Intent Classifier) yapay zekasın. 
Görevin, kullanıcının mesajını analiz edip ne istediğini bulmaktır.
KURALLAR:
1. Eğer kullanıcı Python kodu istiyorsa, veri ön işleme, model eğitimi, grafik çizimi, eksik veri doldurma gibi veri bilimi işlemleri talep ediyorsa SADECE VE SADECE 'KOD' yaz.
2. Eğer kullanıcı genel bir soru soruyorsa SADECE VE SADECE 'SOHBET' yaz.

ÖRNEKLER:
Kullanıcı: 'Bana kalpli bir metin yaz', 'Nasılsın', 'Bana bir hikaye anlat'
AI: SOHBET

Kullanıcı: 'Şu veriyi analiz et', 'Bana bir Python fonksiyonu yaz', 'Grafik çiz'
AI: KOD

Asla açıklama yapma. Yanıtın tek bir kelime olmalı: KOD veya SOHBET.";

            try
            {
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        new { role = "system", content = systemMessage },
                        new { role = "user", content = userMessage }
                    },
                    temperature = 0.1
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode) return "SOHBET"; // varsayım olarak sohbet

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var aiMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim().ToUpper();

                // KOD içeriyorsa KOD, yoksa default SOHBET
                if (aiMessage != null && aiMessage.Contains("KOD"))
                    return "KOD";

                return "SOHBET";
            }
            catch
            {
                return "SOHBET"; // çökerse sohbete dön
            }
        }


    }
}