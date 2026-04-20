using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using FluentValidation; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExecutionController : ControllerBase
    {
        private readonly ICodeExecutionService _executionService;
        private readonly IDatasetProfilerService _profilerService;
        private readonly IExecutionQueueService _queueService;
        private readonly IWorkspaceService _workspaceService;

        public ExecutionController(
            ICodeExecutionService executionService,
            IDatasetProfilerService profilerService,
            IExecutionQueueService queueService,
            IWorkspaceService workspaceService)
        {
            _executionService = executionService;
            _profilerService = profilerService;
            _queueService = queueService;
            _workspaceService = workspaceService;
        }

        [HttpPost("{workspaceId}/run")]
        public async Task<IActionResult> RunCode(
            int workspaceId,
            [FromBody] ExecuteCodeDto request,
            [FromServices] IValidator<ExecuteCodeDto> validator)
        {
            // validator kural
            var validationResult = await validator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(workspaceId, userId);
            if (workspace == null)
            {
                return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });
            }

            // Asenkron iş kuyruğuna atama İPTAL EDİLDİ - SENKRON (BLOKLAYICI) YÜRÜTME YAPILIYOR
            var job = new ExecutionJob
            {
                WorkspaceId = workspaceId,
                Code = request.Code,
                DatasetFileName = request.DatasetFileName
            };

            job.Status = "Processing";
            _queueService.UpdateJob(job); // Durumu memory'e kaydet

            try 
            {
                var result = await _executionService.ExecutePythonCodeAsync(job);
                job.Result = result;
                job.Status = result.IsSuccess ? "Completed" : "Failed";

                // --- EF CORE EXPERIMENT TRACKING ENTEGRASYONU ---
                var dbContext = HttpContext.RequestServices.GetRequiredService<Currere_backend.Data.AppDbContext>();
                string? parsedMetrics = null;
                if (result.IsSuccess)
                {
                    parsedMetrics = Currere_backend.Helpers.MetricsParser.ParseMetrics(result.Output);
                }

                // Hızlı Basit Hash
                using (var sha256Hash = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(job.Code ?? ""));
                    var builder = new System.Text.StringBuilder();
                    foreach (var b in bytes) builder.Append(b.ToString("x2"));
                    
                    var expLog = new ExperimentLog
                    {
                        WorkspaceId = job.WorkspaceId,
                        CodeHash = builder.ToString(),
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
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Result = new ExecutionResultDto
                {
                    IsSuccess = false,
                    Error = $"Sistem Hatası (Sync Execution): {ex.Message}",
                    ErrorType = "InternalApiCrash"
                };
            }
            finally
            {
                _queueService.UpdateJob(job);
            }

            return Accepted(new { jobId = job.JobId, status = job.Status });
        }

        [HttpGet("status/{jobId}")]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            var job = _queueService.GetJobStatus(jobId);
            if (job == null) return NotFound(new { error = "Sistem Hatası: Belirtilen JobId bulunamadı." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(job.WorkspaceId, userId);
            if (workspace == null)
                return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

            if (job.Status == "Processing") 
                return Ok(new { jobId = job.JobId, status = job.Status });

            if (job.Status == "Failed")
            {
                // Result null ise internal failed olmuştur
                return BadRequest(job.Result ?? new ExecutionResultDto { IsSuccess = false, Error = "Kuyruk hatası." });
            }

            return Ok(job.Result);
        }

        [HttpGet("{workspaceId}/profile/{fileName}")]
        public async Task<IActionResult> GetDatasetProfile(int workspaceId, string fileName)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var workspace = await _workspaceService.GetWorkspaceByIdAsync(workspaceId, userId);
                if (workspace == null)
                    return NotFound(new { error = "Çalışma alanı bulunamadı veya erişim yetkiniz yok." });

                var jsonResult = await _profilerService.ProfileDatasetAsync(workspaceId, fileName);

                // Gelen string zaten JSON formatında olduğu için doğrudan Content olarak dönüyoruz
                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}