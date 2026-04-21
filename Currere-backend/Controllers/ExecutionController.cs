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

            // Asenkron iş kuyruğuna atama işlemi
            var job = new ExecutionJob
            {
                WorkspaceId = workspaceId,
                Code = request.Code,
                DatasetFileName = request.DatasetFileName
            };

            await _queueService.EnqueueJobAsync(job);

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
                // Result null ise internal failed olmuştur. Hata olsa bile kullanıcıya terminal çıktısını 
                // gösterebilmek için 200 OK dönüyoruz.
                return Ok(job.Result ?? new ExecutionResultDto { IsSuccess = false, Error = "İşlem başarısız veya kuyruk hatası." });
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