using System.Text;
using System.Text.Json;

namespace Currere_backend.Services
{
    public class NotebookConverterService : INotebookConverterService
    {
        private readonly IAiService _aiService;

        public NotebookConverterService(IAiService aiService)
        {
            _aiService = aiService;
        }

        public Task<string> ExtractRawPythonFromNotebookAsync(string ipynbContent)
        {
            try
            {
                using var document = JsonDocument.Parse(ipynbContent);
                var root = document.RootElement;

                if (!root.TryGetProperty("cells", out var cells))
                {
                    throw new Exception("Geçersiz Notebook formatı: 'cells' dizisi bulunamadı.");
                }

                var rawPythonScript = new StringBuilder();

                foreach (var cell in cells.EnumerateArray())
                {
                    if (cell.TryGetProperty("cell_type", out var cellType) && cellType.GetString() == "code")
                    {
                        if (cell.TryGetProperty("source", out var sourceArray))
                        {
                            foreach (var line in sourceArray.EnumerateArray())
                            {
                                rawPythonScript.Append(line.GetString());
                            }
                            rawPythonScript.AppendLine("\n");
                        }
                    }
                }

                return Task.FromResult(rawPythonScript.ToString());
            }
            catch (JsonException ex)
            {
                throw new Exception($"Notebook dosyası okunamadı, JSON formatı bozuk: {ex.Message}");
            }
        }

        public async Task<string> CleanAndOptimizePythonCodeAsync(string rawPythonCode)
        {
            string systemPrompt = @"Sen kıdemli bir Python Geliştiricisi ve Veri Bilimcisin.
Aşağıda, bir Jupyter Notebook'tan (IPYNB) sırasız bir şekilde çıkarılmış, karmaşık ve ham bir Python kodu var.
GÖREVİN: Bu spagetti kodu analiz edip, baştan sona hatasız çalışacak, temiz ve prodüksiyona hazır SADECE TEK BİR .py scripti haline getirmektir.

KATI KURALLAR:
1. Tekrar eden kütüphane içe aktarımlarını (import) sil ve en üste tek bir blok halinde topla.
2. Gereksiz veya sadece Notebook'ta ekrana basmak için yazılmış (örn: df.head(), print(df)) gibi kalıntıları temizle.
3. Mantıksal çalışma sırasını (yukarıdan aşağıya) düzelt. Değişkenlerin ezilmesini engelle.
4. Çıktı SADECE çalıştırılabilir Python kodu olmalıdır. Kod markdown tagleri (```python ... ```) içinde verilecektir.
Asla açıklama, giriş veya yorum ekleme.";

            try
            {
                // temislik
                var cleanedCode = await _aiService.ChatAsync(rawPythonCode, systemPrompt);

                if (cleanedCode.StartsWith("```python"))
                {
                    cleanedCode = cleanedCode.Replace("```python", "").Replace("```", "").Trim();
                }

                return cleanedCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Yapay zeka kod temizleme sırasında çöktü: {ex.Message}");
            }
        }
    }
}