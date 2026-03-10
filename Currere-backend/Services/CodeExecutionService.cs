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

            try
            {
                // AKILLI İMAJ KONTROLÜ
                var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters());
                bool imageExists = images.Any(i => i.RepoTags != null && i.RepoTags.Contains("python:3.9-slim"));

                if (!imageExists)
                {
                    await _dockerClient.Images.CreateImageAsync(
                        new ImagesCreateParameters { FromImage = "python", Tag = "3.9-slim" },
                        null,
                        new Progress<JSONMessage>());
                }

                var envVars = new List<string> { $"CODE_TO_RUN={code}" };

                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "python:3.9-slim",
                    Env = envVars,
                    Cmd = new List<string> { "/bin/sh", "-c", "echo \"$CODE_TO_RUN\" > script.py && python script.py" },
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
                var waitResponse = await _dockerClient.Containers.WaitContainerAsync(containerId, default);

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
                    Error = $"Sistem Hatası: {ex.Message}",
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