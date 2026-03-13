using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Currere_backend.DTOs;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Hosting;

namespace Currere_backend.Services
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly DockerClient _dockerClient;
        private readonly IWebHostEnvironment _env;

        public CodeExecutionService(IWebHostEnvironment env)
        {
            _dockerClient = new DockerClientConfiguration().CreateClient();
            _env = env;
        }

        public async Task<ExecutionResultDto> ExecutePythonCodeAsync(int workspaceId, string code)
        {
            var stopwatch = Stopwatch.StartNew();
            var containerId = string.Empty;

            // FİZİKSEL KLASÖR YOLUNU BULMA
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var hostWorkspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            if (!Directory.Exists(hostWorkspacePath))
            {
                Directory.CreateDirectory(hostWorkspacePath);
            }

            // DOCKER İÇİN PATH DÜZELTMESİ (Windows '\' karakterini '/' yapar)
            var dockerBindPath = hostWorkspacePath.Replace("\\", "/");


            try
            {
                // AKILLI İMAJ KONTROLÜ
                // slim modunda numpy pandas yok ve biz interneti keserek sandbox kuruyoruz
                // internet de olmayınca py kısmında numpy pandas indiremiyor
                // her defasında indirirsek de sistem optimizasyonu bozulur
                // kendimiz bir imaj olusturup numpy pandası onun icine atacagiz
                var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters());
                bool imageExists = images.Any(i => i.RepoTags != null && i.RepoTags.Contains("currere-sandbox:latest"));

                if (!imageExists)
                {
                    // imajı artık dockerhubtan degil
                    // kendimizden cekip hata yolluyoruz
                    throw new Exception("Sistem Hatası: 'currere-sandbox:latest' imajı bulunamadı. Lütfen terminalden 'docker build -t currere-sandbox:latest .' komutu ile imajı inşa edin.");
                }

                // py wrapper ve base64 korumasi
                // Kullanıcı kodundaki tırnak ve boşlukların bash/python'u patlatmaması için base64 ile şifreliyoruz
                var base64Code = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(code));

                string wrappedCode = $@"
import sys, json, io, traceback, base64

old_stdout = sys.stdout
redirected_output = io.StringIO()
sys.stdout = redirected_output

try:
    # Kodu Base64'ten çöz ve çalıştır
    user_code = base64.b64decode('{base64Code}').decode('utf-8')
    exec(user_code, globals())
    
    sys.stdout = old_stdout
    user_output = redirected_output.getvalue()
    
    print(json.dumps({{
        'success': True,
        'error_type': None,
        'message': user_output.strip()
    }}))
except Exception as e:
    sys.stdout = old_stdout
    error_type = type(e).__name__
    error_msg = str(e)
    
    print(json.dumps({{
        'success': False,
        'error_type': error_type,
        'message': error_msg
    }}))
";

                var envVars = new List<string> { $"CODE_TO_RUN={wrappedCode}" };

                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "currere-sandbox:latest",
                    Env = envVars,
                    // veriyi algılayamıyor, temp e atıyoruz 
                    // printenv komutu
                    Cmd = new List<string> { "/bin/sh", "-c", "printenv CODE_TO_RUN > /tmp/script.py && python /tmp/script.py" },
                    WorkingDir = "/workspace",
                    HostConfig = new HostConfig
                    {
                        Memory = 1024 * 1024 * 1024, // 512 mb ram'e yükselttik çünkü kütüphaneleri karsilayamiyor
                        // hala yetmiyor 1gb yaptık
                        NetworkMode = "none",       // interneti kes
                        AutoRemove = false,
                        Binds = new List<string> { $"{dockerBindPath}:/workspace:ro" } // read only'e çevirdik
                    }
                });

                containerId = response.ID;

                // konteynerı başlatma
                await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());

                // timeout için 10 saniye
                // 10 saniye yetersiz kaldı 30 saniye test
                // bunu üstten alıp alta taşıdık  timeojt yememesi için cünkü diğer türlü dockerın başlamasından itibaren alıyordu
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

                // arkada gelen promot/kodun compile olmasını bekliyoruz
                var waitResponse = await _dockerClient.Containers.WaitContainerAsync(containerId, cts.Token);

                // logları okuma
                using var logsStream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, false, new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true
                });

                (string stdout, string stderr) = await logsStream.ReadOutputToEndAsync(default);
                stopwatch.Stop();

                // json cozumleme
                string rawOutput = stdout?.Trim() ?? "";
                bool isSuccess = false;
                string finalOutput = "";
                string errorType = "";

                try
                {
                    var jsonResult = JsonDocument.Parse(rawOutput).RootElement;
                    isSuccess = jsonResult.GetProperty("success").GetBoolean();
                    finalOutput = jsonResult.GetProperty("message").GetString() ?? "";

                    if (!isSuccess)
                    {
                        errorType = jsonResult.GetProperty("error_type").GetString() ?? "UnknownError";
                    }
                }
                catch
                {
                    // Docker json donmeden çökerse
                    isSuccess = false;
                    finalOutput = rawOutput + "\n" + (stderr?.Trim() ?? "");
                    errorType = "SystemCrashError";
                }

                return new ExecutionResultDto
                {
                    Output = isSuccess ? finalOutput : "",
                    Error = !isSuccess ? finalOutput : "",
                    ErrorType = errorType,
                    IsSuccess = isSuccess,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException || ex is TimeoutException || ex.Message.Contains("timed out"))
            {
                // timeout yakalandı
                stopwatch.Stop();
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = "Sistem Hatası: Kodun çalışma süresi 30 saniyelik limiti aştı. Kodda sonsuz döngü (infinite loop) veya çok ağır bir işlem var.",
                    ErrorType = "TimeoutError",
                    IsSuccess = false,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                // genel sistem hataları
                stopwatch.Stop();
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = $"Sistem Hatası: {ex.Message}",
                    ErrorType = "SystemError",
                    IsSuccess = false,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                // cleaning, sıfır risk
                if (!string.IsNullOrEmpty(containerId))
                {
                    try
                    {
                        await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
                    }
                    catch { /* Temizlik sırasında hata olursa yut */ }
                }
            }
        }
    }
}