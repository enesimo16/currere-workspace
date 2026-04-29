using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Currere_backend.Services
{
    public class NotebookConverterService : INotebookConverterService
    {
        private readonly IAiService _aiService;

        // Jupyter magic komutları ve shell komutları — .py'de anlamsız
        private static readonly string[] MagicPrefixes = ["%", "!", "??", "?"];

        public NotebookConverterService(IAiService aiService)
        {
            _aiService = aiService;
        }

        // IPYNB dosyasından sadece code cell'lerini çeker,
        // magic komutları filtreler ve ham Python kodu döner.
  
        public Task<string> ExtractRawPythonFromNotebookAsync(string ipynbContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipynbContent))
                    throw new FormatException("Geçersiz veya bozuk Notebook formatı. Lütfen geçerli bir .ipynb içeriği sağlayın.");

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(ipynbContent);
                }
                catch
                {
                    throw new FormatException("Geçersiz veya bozuk Notebook formatı. Lütfen geçerli bir .ipynb içeriği sağlayın.");
                }

                using (document)
                {
                    var root = document.RootElement;

                    if (!root.TryGetProperty("cells", out var cells))
                        throw new FormatException("Geçersiz veya bozuk Notebook formatı. Lütfen geçerli bir .ipynb içeriği sağlayın.");

                var rawPythonScript = new StringBuilder();
                int cellIndex = 0;

                foreach (var cell in cells.EnumerateArray())
                {
                    if (!cell.TryGetProperty("cell_type", out var cellType))
                        continue;

                    var type = cellType.GetString();

                    if (type == "code")
                    {
                        var cellCode = ExtractSourceFromCell(cell);

                        if (string.IsNullOrWhiteSpace(cellCode))
                            continue;

                        // Magic ve shell komutlarını filtrele
                        var filteredCode = FilterMagicCommands(cellCode);

                        if (!string.IsNullOrWhiteSpace(filteredCode))
                        {
                            rawPythonScript.AppendLine($"# --- Cell {++cellIndex} ---");
                            rawPythonScript.AppendLine(filteredCode.TrimEnd());
                            rawPythonScript.AppendLine(); // tek boş satır
                        }
                    }
                    // Markdown cell'leri tamamen yoksay
                }

                var finalScript = rawPythonScript.ToString();
                if (string.IsNullOrWhiteSpace(finalScript))
                    throw new FormatException("Geçersiz veya bozuk Notebook formatı. Lütfen geçerli bir .ipynb içeriği sağlayın.");

                return Task.FromResult(finalScript);
                }
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FormatException("Geçersiz veya bozuk Notebook formatı. Lütfen geçerli bir .ipynb içeriği sağlayın.", ex);
            }
        }

        // Hem string hem de string[] formatındaki 'source' alanını destekler.
        private static string ExtractSourceFromCell(JsonElement cell)
        {
            if (!cell.TryGetProperty("source", out var source))
                return string.Empty;

            // Bazı notebook versiyonları source'u tek string verir
            if (source.ValueKind == JsonValueKind.String)
                return source.GetString() ?? string.Empty;

            // Çoğunlukla string array
            if (source.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var line in source.EnumerateArray())
                    sb.Append(line.GetString());
                return sb.ToString();
            }

            return string.Empty;
        }

        // %matplotlib, !pip install gibi Jupyter'a özgü komutları kaldırır.
        // Inline magic'leri (örn: %timeit) yorum satırına dönüştürür.
 
        private static string FilterMagicCommands(string code)
        {
            var lines = code.Split('\n');
            var result = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                // Shell komutu (!pip, !apt vs.) → tamamen sil
                if (trimmed.StartsWith("!"))
                {
                    result.Add($"# [KALDIRILDI - Shell komutu]: {trimmed}");
                    continue;
                }

                // Cell-level magic (%matplotlib, %%time vs.) → yorum satırı yap
                if (trimmed.StartsWith("%%") || trimmed.StartsWith("%"))
                {
                    result.Add($"# [KALDIRILDI - Jupyter magic]: {trimmed}");
                    continue;
                }

                // Jupyter'a özgü display fonksiyonları → yorum satırı yap
                if (Regex.IsMatch(trimmed, @"^(display|HTML|Markdown|Image|Audio|Video)\s*\("))
                {
                    result.Add($"# [KALDIRILDI - Jupyter display]: {trimmed}");
                    continue;
                }

                result.Add(line);
            }

            return string.Join('\n', result);
        }

        // Ham Python kodunu AI ile temizler, tekrarlı import'ları birleştirir,
        // gereksiz notebook kalıntılarını kaldırır ve üretime hazır hale getirir.
        public async Task<string> CleanAndOptimizePythonCodeAsync(string rawPythonCode)
        {
            string systemPrompt = @"Sen kıdemli bir Python Geliştiricisi ve Veri Bilimcisin.
Aşağıda, bir Jupyter Notebook'tan çıkarılmış ham Python kodu var.
 
GÖREVİN: Bu kodu baştan sona hatasız çalışacak, temiz ve üretime hazır TEK BİR .py scripti haline getirmektir.
 
KATI KURALLAR:
1. Tüm import ifadelerini tekrarsız şekilde dosyanın EN ÜSTÜNE taşı.
2. Sadece Notebook'ta ekrana basmak için yazılmış geçici ifadeleri (df.head(), df.info(), display(), print(df)) sil.
3. Mantıksal çalışma sırasını (yukarıdan aşağıya) düzelt; aynı değişkenin gereksiz kez ezilmesini önle.
4. Dosya okuma / veri yükleme satırlarını (pd.read_csv, open(), vs.) ASLA silme veya değiştirme.
5. # --- Cell N --- gibi yapısal yorumları temizle; anlamlı yorum yoksa hiç yorum ekleme.
6. ÇIKTI SADECE çalıştırılabilir Python kodu olmalı. Kod ```python ... ``` tagları içinde ver.
7. Açıklama, giriş metni veya özet YAZMA.";

            try
            {
                var cleanedCode = await _aiService.ChatAsync(rawPythonCode, systemPrompt);
                return StripMarkdownFences(cleanedCode);
            }
            catch (Exception ex)
            {
                throw new Exception($"Yapay zeka kod temizleme sırasında hata oluştu: {ex.Message}");
            }
        }
        // AI'ın döndürdüğü yanıttan ```python ... ``` veya ``` ... ``` bloğunu soyar.
        public static string StripMarkdownFences(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
                return aiResponse;

            // ```python\n...\n``` veya ```\n...\n``` formatlarını yakala
            var match = Regex.Match(
                aiResponse,
                @"```(?:python)?\s*\n([\s\S]*?)\n?```",
                RegexOptions.IgnoreCase
            );

            return match.Success
                ? match.Groups[1].Value.Trim()
                : aiResponse.Trim();
        }
    }
}
