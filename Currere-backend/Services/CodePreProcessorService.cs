using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace Currere_backend.Services
{
    public class CodePreProcessorService : ICodePreProcessorService
    {
        private readonly IWebHostEnvironment _env;

        public CodePreProcessorService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<CodePreProcessResultDto> ProcessCodeAsync(string rawCode)
        {
            // Olası kök dizin yaklaşımları
            var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Helpers", "SecurityPreprocessor.py");

            if (!File.Exists(scriptPath))
            {
                // Eğer Environment kullanmamız gerekirse
                var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var projectRoot = Directory.GetParent(webRootPath)?.FullName ?? Directory.GetCurrentDirectory();
                scriptPath = Path.Combine(projectRoot, "Helpers", "SecurityPreprocessor.py");

                if (!File.Exists(scriptPath))
                {
                    throw new Exception("Sistem Hatası: SecurityPreprocessor.py dosyası bulunamadı.");
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,   // Giren kod
                StandardOutputEncoding = Encoding.UTF8,  // Çıkan kod
                StandardErrorEncoding = Encoding.UTF8    // Çıkan hata
            };

            using var process = new Process { StartInfo = startInfo };
            
            try
            {
                process.Start();

                // Tüm veriyi stdin'e yazıp kapatıyoruz, böylece Python betiği okumayı bitirir.
                await process.StandardInput.WriteAsync(rawCode);
                process.StandardInput.Close();

                // Çıktı ve hataları eşzamanlı olarak oku
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Tarama işlemi timeout (10 sn)
                var waitResult = process.WaitForExit(10000);
                if (!waitResult)
                {
                    process.Kill();
                    throw new Exception("Sistem Hatası: Güvenlik tarayıcısı yanıt vermedi (Timeout).");
                }

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    // Python betiği hata kodu döndürdüyse (Güvenlik İhlali veya Syntax Error)
                    string errorMsg = !string.IsNullOrWhiteSpace(error) ? error.Trim() : "Bilinmeyen Güvenlik Tarayıcı Hatası.";
                    throw new Exception(errorMsg);
                }

                try
                {
                    var result = JsonSerializer.Deserialize<CodePreProcessResultDto>(output, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (result == null) throw new Exception("JSON parse hatası: Çıktı boş.");
                    return result;
                }
                catch
                {
                    throw new Exception("Sistem Hatası: Güvenlik tarayıcısından beklenen formatta veri gelmedi.");
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.StartsWith("Güvenlik İhlali") || ex.Message.StartsWith("Sözdizimi Hatası") || ex.Message.StartsWith("JSON"))
                {
                    // Kullanıcıya gösterilecek temiz hata
                    throw new Exception(ex.Message); 
                }
                
                // Sistem hatalarını sakla / dönüştür
                throw new Exception($"Sistem Hatası: Güvenlik taraması sırasında bir hata oluştu: {ex.Message}");
            }
        }
    }
}
