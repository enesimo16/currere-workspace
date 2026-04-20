using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Currere_backend.Models;
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
        private readonly ICodePreProcessorService _codePreProcessorService;

        // Constructor güncellendi
        public CodeExecutionService(IWebHostEnvironment env, IHubContext<TerminalHub> hubContext, ICodePreProcessorService codePreProcessorService)
        {
            _dockerClient = new DockerClientConfiguration().CreateClient();
            _env = env;
            _hubContext = hubContext;
            _codePreProcessorService = codePreProcessorService;
        }

        public async Task<ExecutionResultDto> ExecutePythonCodeAsync(ExecutionJob job)
        {
            var stopwatch = Stopwatch.StartNew();
            int workspaceId = job.WorkspaceId;
            string code = job.Code?.Replace("\uFEFF", "").Replace("\r", "") ?? ""; // BOM ve Windows CRLF temizleme
            CodePreProcessResultDto preProcessResult;
            try
            {
                // STATİK AST GÜVENLİK TARAMASI VE IPYNB PARSER
                preProcessResult = await _codePreProcessorService.ProcessCodeAsync(code);
                code = preProcessResult.Code;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = ex.Message,
                    ErrorType = "SecurityOrSyntaxError",
                    IsSuccess = false,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

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

                // DİNAMİK BAĞIMLILIK KURULUMU (Ön Kurulum Konteyneri)
                if (preProcessResult.Dependencies != null && preProcessResult.Dependencies.Any())
                {
                    var installCmd = new List<string> { "pip", "install", "-t", "/workspace/site-packages" };
                    installCmd.AddRange(preProcessResult.Dependencies);
                    
                    var installerResponse = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                    {
                        Image = "currere-sandbox:latest",
                        Name = $"currere-install-{job.JobId}", // JobId tabanlı benzersiz isim
                        Cmd = installCmd,
                        WorkingDir = "/workspace",
                        HostConfig = new HostConfig
                        {
                            NetworkMode = "bridge", // İnternet açık
                            Binds = new List<string> { $"{dockerBindPath}:/workspace" } // Kurulum için okuma/yazma açık
                        }
                    });

                    await _dockerClient.Containers.StartContainerAsync(installerResponse.ID, new ContainerStartParameters());
                    
                    // Kurulumun bitmesini bekle (maksimum 45 saniye)
                    using var installCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    await _dockerClient.Containers.WaitContainerAsync(installerResponse.ID, installCts.Token);
                    
                    // Temizlik (geçici kurucu konteyner)
                    await _dockerClient.Containers.RemoveContainerAsync(installerResponse.ID, new ContainerRemoveParameters { Force = true });
                }

                // py wrapper ve base64 korumasi
                // Kullanıcı kodundaki tırnak ve boşlukların bash/python'u patlatmaması için base64 ile şifreliyoruz
                var codeBytes = new System.Text.UTF8Encoding(false).GetBytes(code);
                // Son bir çare: Eğer hala gizli bir BOM (EF BB BF) varsa, byte array üzerinden manuel kes:
                if (codeBytes.Length >= 3 && codeBytes[0] == 0xEF && codeBytes[1] == 0xBB && codeBytes[2] == 0xBF)
                {
                    var newBytes = new byte[codeBytes.Length - 3];
                    Array.Copy(codeBytes, 3, newBytes, 0, newBytes.Length);
                    codeBytes = newBytes;
                }
                var base64Code = Convert.ToBase64String(codeBytes);

                // Artık Python kodunu C# içinde yollamıyoruz, sadece Base64 metnini değişkene veriyoruz
                // Yeni: Ön kurulum yapılan site-packages klasörünü PYTHONPATH'e ekliyoruz
                var envVars = new List<string> { 
                    $"CODE_TO_RUN={base64Code}",
                    "PYTHONPATH=/workspace/site-packages"
                };

                // ARTIFACT OUTPUT MAPPING
                var hostOutputPath = Path.Combine(webRootPath, "artifacts", job.JobId);
                if (!Directory.Exists(hostOutputPath))
                {
                    Directory.CreateDirectory(hostOutputPath);
                }
                var dockerOutputBind = hostOutputPath.Replace("\\", "/");

                var bindsList = new List<string> { 
                    $"{dockerBindPath}:/workspace:rw",  // Workspace Root RW
                    $"{dockerOutputBind}:/workspace/output:rw" // Artifact Output RW
                };

                // DATASET MAPPING
                if (!string.IsNullOrWhiteSpace(job.DatasetFileName))
                {
                    // FileService dosyaları workspace {id} root dizinine kaydediyor
                    var datasetHostPath = Path.Combine(hostWorkspacePath, job.DatasetFileName);
                    if (File.Exists(datasetHostPath))
                    {
                        var dockerDatasetBind = datasetHostPath.Replace("\\", "/");
                        bindsList.Add($"{dockerDatasetBind}:/workspace/data/{job.DatasetFileName}:ro");
                    }
                    else
                    {
                        stopwatch.Stop();
                        return new ExecutionResultDto
                        {
                            Output = "",
                            Error = $"Sistem Hatası: Belirtilen veri seti bulunamadı: {job.DatasetFileName}. Lütfen klasörü kontrol edin.",
                            ErrorType = "DatasetNotFoundError",
                            IsSuccess = false,
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        };
                    }
                }

                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "currere-sandbox:latest",
                    Name = $"currere-run-{job.JobId}", // JobId tabanlı benzersiz isim
                    Env = envVars,
                    Cmd = new List<string> { "python", "-u", "/app/runner.py" },
                    WorkingDir = "/workspace",
                    HostConfig = new HostConfig
                    {
                        Memory = 512L * 1024L * 1024L, // 512MB RAM Limit
                        NanoCPUs = 500000000,          // 0.5 CPU Limit (Koruması)
                        NetworkMode = "none",       // interneti kes
                        AutoRemove = false,
                        Binds = bindsList
                    }
                });

                containerId = response.ID;

                // konteynerı başlatma
                await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());

                // timeout için 15 saniye zorunlu kılındı
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

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
                
                // OOM (Out of Memory) Kontrolü
                var containerInfo = await _dockerClient.Containers.InspectContainerAsync(containerId);
                if (containerInfo.State.OOMKilled)
                {
                    stopwatch.Stop();
                    return new ExecutionResultDto
                    {
                        Output = "",
                        Error = "Out of Memory: İşlem çok fazla bellek tüketti (512MB Sınırı).",
                        ErrorType = "OutOfMemoryError",
                        IsSuccess = false,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

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
                        if (finalOutput.Contains("Traceback (most recent call last)") || finalOutput.Contains("File \""))
                        {
                            finalOutput = ParsePythonTraceback(finalOutput, ref errorType);
                        }
                    }
                }
                catch
                {
                    // Docker json donmeden çökerse
                    isSuccess = false;
                    finalOutput = rawOutput + "\n" + (stderr?.Trim() ?? "");
                    errorType = "SystemCrashError";

                    if (finalOutput.Contains("Traceback (most recent call last)") || finalOutput.Contains("File \""))
                    {
                        finalOutput = ParsePythonTraceback(finalOutput, ref errorType);
                    }
                }

                // ARTIFACT URL COLLECTION
                var artifactUrls = new List<string>();
                if (Directory.Exists(hostOutputPath))
                {
                    var files = Directory.GetFiles(hostOutputPath);
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        artifactUrls.Add($"/artifacts/{job.JobId}/{fileName}");
                    }
                }

                return new ExecutionResultDto
                {
                    Output = isSuccess ? finalOutput : "",
                    Error = !isSuccess ? finalOutput : "",
                    ErrorType = errorType,
                    IsSuccess = isSuccess,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ArtifactUrls = artifactUrls
                };
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                // timeout yakalandı
                stopwatch.Stop();
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = "Time-out: Kod 15 saniyeden uzun sürdü ve durduruldu.",
                    ErrorType = "TimeoutError",
                    IsSuccess = false,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                // genel sistem hataları
                stopwatch.Stop();
                var innerMsg = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : "";
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = $"Sistem Hatası: {ex.Message}{innerMsg}",
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

        private string ParsePythonTraceback(string rawError, ref string errorType)
        {
            if (string.IsNullOrWhiteSpace(rawError)) return rawError;

            // Satır tespiti
            int lineNumber = 0;
            var lineMatches = System.Text.RegularExpressions.Regex.Matches(rawError, @"line\s+(\d+)");
            if (lineMatches.Count > 0)
            {
                int.TryParse(lineMatches[lineMatches.Count - 1].Groups[1].Value, out lineNumber);
            }

            // Hata mesajı genelde son satırdadır
            var lines = rawError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string lastLine = lines.LastOrDefault()?.Trim() ?? "";

            string errorMessage = lastLine;
            var errorMatch = System.Text.RegularExpressions.Regex.Match(lastLine, @"^([\w\.]+Error|Exception|Warning|SyntaxError):\s*(.*)$");
            if (errorMatch.Success)
            {
                errorType = errorMatch.Groups[1].Value;
                errorMessage = errorMatch.Groups[2].Value;
            }

            var smartObj = new
            {
                ErrorType = errorType,
                LineNumber = lineNumber,
                ErrorMessage = errorMessage
            };

            return JsonSerializer.Serialize(smartObj);
        }
    }
}