using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Currere_backend.Models;
using Currere_backend.DTOs;
using Currere_backend.Data;
using Currere_backend.Helpers;

namespace Currere_backend.Services
{
    public class ExecutionWorker : BackgroundService
    {
        private readonly IExecutionQueueService _queueService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExecutionWorker> _logger;

        public ExecutionWorker(
            IExecutionQueueService queueService,
            IServiceProvider serviceProvider,
            ILogger<ExecutionWorker> logger)
        {
            _queueService = queueService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Execution Worker arka planda calismaya basladi.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _queueService.DequeueJobAsync(stoppingToken);

                    // Her bir iş için yeni bir Scope yaratıyoruz (Çünkü ICodeExecutionService Scoped olarak kayıtlı)
                    using var scope = _serviceProvider.CreateScope();
                    var executionService = scope.ServiceProvider.GetRequiredService<ICodeExecutionService>();

                    try
                    {
                        var result = await executionService.ExecutePythonCodeAsync(job);
                        
                        job.Result = result;
                        job.Status = result.IsSuccess ? "Completed" : "Failed";

                        // --- EF CORE EXPERIMENT TRACKING ENTEGRASYONU ---
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        string? parsedMetrics = null;
                        if (result.IsSuccess)
                        {
                            parsedMetrics = MetricsParser.ParseMetrics(result.Output);
                        }

                        var expLog = new ExperimentLog
                        {
                            WorkspaceId = job.WorkspaceId,
                            CodeHash = ComputeHash(job.Code),
                            CodeContent = job.Code,
                            DatasetReference = job.DatasetFileName,
                            ExecutionDurationMs = result.ExecutionTimeMs,
                            OutputMetrics = parsedMetrics,
                            ArtifactUrls = result.ArtifactUrls != null && result.ArtifactUrls.Count > 0 
                                ? System.Text.Json.JsonSerializer.Serialize(result.ArtifactUrls) 
                                : null,
                            IsSuccess = result.IsSuccess
                        };

                        dbContext.ExperimentLogs.Add(expLog);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        // ------------------------------------------------
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"JobId {job.JobId} calistirilirken beklenmeyen bir hata olustu.");
                        job.Status = "Failed";
                        job.Result = new ExecutionResultDto
                        {
                            IsSuccess = false,
                            Error = "Sistem Hatası: Kuyruktan işlenirken arka planda beklenmeyen bir sorun meydana geldi.",
                            ErrorType = "InternalQueueError"
                        };
                    }
                    finally
                    {
                        _queueService.UpdateJob(job);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Uygulama kapanıyor
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kuyruktan is alinirken bir sorun olustu.");
                }
            }
        }

        private string ComputeHash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
