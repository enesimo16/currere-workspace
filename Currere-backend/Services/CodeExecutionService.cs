using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Currere_backend.DTOs;
using Currere_backend.Hubs; 
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR; 

namespace Currere_backend.Services
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly DockerClient _dockerClient;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<TerminalHub> _hubContext; // hubı aldık

        // Constructor güncellendi
        public CodeExecutionService(IWebHostEnvironment env, IHubContext<TerminalHub> hubContext)
        {
            _dockerClient = new DockerClientConfiguration().CreateClient();
            _env = env;
            _hubContext = hubContext;
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

                // Artık Python kodunu C# içinde yollamıyoruz, sadece Base64 metnini değişkene veriyoruz
                var envVars = new List<string> { $"CODE_TO_RUN={base64Code}" };

                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "currere-sandbox:latest",
                    Env = envVars,
                    // veriyi algılayamıyor, temp e atıyoruz 
                    // printenv komutu (Artık imajın içine gömdüğümüz runner.py dosyasını çalıştırıyoruz)
                    // bloklar halinde dönmemesi icin
                    Cmd = new List<string> { "python", "-u", "/app/runner.py" },
                    WorkingDir = "/workspace",
                    HostConfig = new HostConfig
                    {
                        Memory = 1024L * 1024L * 1024L, // hala yetmiyor 1gb yaptık
                        NetworkMode = "none",       // interneti kes
                        AutoRemove = false,
                        Binds = new List<string> { $"{dockerBindPath}:/workspace:ro" } // read only'e çevirdik
                    }
                });

                containerId = response.ID;

                // konteynerı başlatma
                await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());

                // timeout için 10 saniye yetersiz kaldı 30 saniye test
                // bunu üstten alıp alta taşıdık  timeojt yememesi için cünkü diğer türlü dockerın başlamasından itibaren alıyordu
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

                // logları okuma
                // akısı saglamak icin follow true
                using var logsStream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, false, new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true
                });

                var stdoutBuilder = new System.Text.StringBuilder();
                var stderrBuilder = new System.Text.StringBuilder();

                // signalr ile 
                // loglama akışı
                // kullanıcı epochları beklerken bir şey yapmadığımızı sanmasın diye
                // cünkü sandbox yavs calisabiliyor
                var logTask = Task.Run(async () =>
                {
                    var buffer = new byte[4096];
                    while (true)
                    {
                        var result = await logsStream.ReadOutputAsync(buffer, 0, buffer.Length, cts.Token);
                        if (result.EOF) break;

                        string chunk = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

                        if (result.Target == MultiplexedStream.TargetStream.StandardError)
                            stderrBuilder.Append(chunk);
                        else
                            stdoutBuilder.Append(chunk);

                        // signalr akış
                        await _hubContext.Clients.Group(workspaceId.ToString()).SendAsync("ReceiveLog", chunk);
                    }
                }, cts.Token);

                // arkada gelen promot/kodun compile olmasını bekliyoruz
                var waitResponse = await _dockerClient.Containers.WaitContainerAsync(containerId, cts.Token);

                // Log okuma işleminin de bitmesini garantiye alıyoruz
                await logTask;

                string stdout = stdoutBuilder.ToString();
                string stderr = stderrBuilder.ToString();
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
                    Error = "Sistem Hatası: Kodun çalışma süresi 120 saniyelik limiti aştı. Kodda sonsuz döngü (infinite loop) veya çok ağır bir işlem var.",
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