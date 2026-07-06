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
            _apiKey = config["AiSettings:GroqApiKey"] ?? throw new Exception("Groq API Anahtarı bulunamadı!");
            // O-1 Fix: DefaultRequestHeaders MUTASYONU KALDIRILDI.
            // Singleton HttpClient'a global header atamak thread-safe değil.
            // Her istek için CreateGroqRequest() ile per-request header ekleniyor.
        }

        // O-1 Fix: Her istek için Authorization header'lı HttpRequestMessage üretir.
        // Bu sayede Singleton HttpClient'a global yazılmıyor — tam thread-safety.
        private HttpRequestMessage CreateGroqRequest(object requestBody)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
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

            using var request = CreateGroqRequest(requestBody);
            var response = await _httpClient.SendAsync(request);

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

            using var request = CreateGroqRequest(requestBody);
            var response = await _httpClient.SendAsync(request);

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

        public async Task<string> GenerateInlineCompletionAsync(string code, int cursorLine, int cursorCol)
        {
            try
            {
                // D-3 Fix: Tüm dosyayı API'ye GÖNDERMEN! İmleç çevresindeki pencereyi al.
                // Eski: 5000 satırlık dosya → her tuşta binlerce token israf.
                // Yeni: imleç öncesi max 1000 karakter + imleç sonrası max 500 karakter.
                var contextBefore = string.Empty;
                var contextAfter = string.Empty;

                if (!string.IsNullOrEmpty(code))
                {
                    // Satır/sütundan karakter offsetini hesapla
                    var lines = code.Split('\n');
                    int cursorCharIndex = 0;
                    int targetLine = Math.Max(0, Math.Min(cursorLine - 1, lines.Length - 1));

                    for (int i = 0; i < targetLine; i++)
                        cursorCharIndex += lines[i].Length + 1; // +1 for \n
                    cursorCharIndex += Math.Min(cursorCol - 1, lines[targetLine].Length);
                    cursorCharIndex = Math.Clamp(cursorCharIndex, 0, code.Length);

                    // Pencereyi kes
                    var beforeStart = Math.Max(0, cursorCharIndex - 1000);
                    contextBefore = code.Substring(beforeStart, cursorCharIndex - beforeStart);

                    var afterEnd = Math.Min(code.Length, cursorCharIndex + 500);
                    contextAfter = code.Substring(cursorCharIndex, afterEnd - cursorCharIndex);
                }

                string systemMessage = @"Sen bir kod tamamlama asistanısın (inline completion). 
Sana çıktıların verilecek: 1) İmleç Öncesi Kod (cursor'dan önceki kısım), 2) İmleç Sonrası Kod (cursor'dan sonraki kısım).
Görevin: ikisi arasına girecek SADECE eksik kodu üretmek.
Kural: Çıktın SADECE eklenecek kod parca sı olmalıdır. Asla açıklama yapma, Markdown kullanma.";

                string userPrompt = $"İmleç Öncesi Kod:\n{contextBefore}\n\n--- İMLEÇ BURASI ---\n\nİmleç Sonrası Kod:\n{contextAfter}\n\nLütfen sadece ortasına girecek devam kodunu yaz.";

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

                using var request = CreateGroqRequest(requestBody);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return ""; // Autocomplete'ın çökmesini istemeyiz, boş dönsün
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                var aiMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                // Markdown işaretlerini temizle
                if (aiMessage != null)
                {
                    aiMessage = aiMessage.Replace("```python", "").Replace("```", "").TrimStart('\n');
                }

                return aiMessage ?? "";
            }
            catch (Exception)
            {
                return "";
            }
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

                using var request = CreateGroqRequest(requestBody);
                var response = await _httpClient.SendAsync(request);

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