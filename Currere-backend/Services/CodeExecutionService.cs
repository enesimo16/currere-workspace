using System.Diagnostics;
using Currere_backend.DTOs;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Currere_backend.Services
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly DockerClient _dockerClient;

        public CodeExecutionService()
        {
            // Windows, Mac veya Linux'a  Docker Daemon ile bağlanıyoruz
            // bu kısımda da docker.dotnet nuget paketini kullanıyoruz
            _dockerClient = new DockerClientConfiguration().CreateClient();
        }

        public async Task<ExecutionResultDto> ExecutePythonCodeAsync(string code)
        {
            var stopwatch = Stopwatch.StartNew();
            var containerId = string.Empty;

            try
            {
                // python:3.9-slim imaj kontrolü , var/yok
                await _dockerClient.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = "python", Tag = "3.9-slim" },
                    null,
                    new Progress<JSONMessage>());

                // kodu docker'a aktarabilmek için env.
                var envVars = new List<string> { $"CODE_TO_RUN={code}" };

                // konteynerı oluşturuyoryuz fakat bu kısımda kısıtlamalar koyuyoruz
                // ram kısıtlaması
                // ve kullanıcının sisteme erişip overrisk komutlarını çalıştırmasını engelliyoruz
                var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = "python:3.9-slim",
                    Env = envVars,
                    Cmd = new List<string> { "/bin/sh", "-c", "echo \"$CODE_TO_RUN\" > script.py && python script.py" },
                    HostConfig = new HostConfig
                    {
                        Memory = 128 * 1024 * 1024, // max 128 mb ram ayırıyoruz
                        NetworkMode = "none",       // aksi halde interneti kesme
                        AutoRemove = false
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

                // MultiplexedStream 
                var (stdout, stderr) = await logsStream.ReadOutputToEndAsync(default);

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