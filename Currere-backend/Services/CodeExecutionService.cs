using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            // timeout için 10 saniye
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

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

                var envVars = new List<string> { $"CODE_TO_RUN={code}" };

                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "currere-sandbox:latest",
                    Env = envVars,
                    // veriyi algılayamıyor, temp e atıyoruz
                    Cmd = new List<string> { "/bin/sh", "-c", "echo \"$CODE_TO_RUN\" > /tmp/script.py && python /tmp/script.py" },
                    WorkingDir = "/workspace",
                    HostConfig = new HostConfig
                    {
                        Memory = 128 * 1024 * 1024, // max 128 mb ram
                        NetworkMode = "none",       // interneti kes
                        AutoRemove = false,
                        Binds = new List<string> { $"{dockerBindPath}:/workspace:ro" } // read only'e çevirdik
                    }
                });

                containerId = response.ID;

                // konteynerı başlatma
                await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());

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

                return new ExecutionResultDto
                {
                    Output = stdout?.Trim() ?? "",
                    Error = stderr?.Trim() ?? "",
                    IsSuccess = waitResponse.StatusCode == 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ExecutionResultDto
                {
                    Output = "",
                    // ipynb to py için kod döngüsü esnasında hata olursa onu detaylı bildirebilmesi için
                    Error = "Sistem Hatası: Kodun çalışma süresi 10 saniyelik limiti aştı. Kodda sonsuz döngü (infinite loop), çok uzun sleep() komutları veya çok ağır bir işlem var. Lütfen kodu optimize et ve bekleme sürelerini kısalt.",
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