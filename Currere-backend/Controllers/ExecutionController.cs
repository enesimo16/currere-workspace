using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using FluentValidation; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public ExecutionController(
            ICodeExecutionService executionService,
            IDatasetProfilerService profilerService,
            IExecutionQueueService queueService)
        {
            _executionService = executionService;
            _profilerService = profilerService;
            _queueService = queueService;
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

        [AllowAnonymous] // Varsa yetki ayarlarına göre değişir, şimdilik böyle. Global [Authorize] varsa bunu korur.
        [HttpGet("status/{jobId}")]
        public IActionResult GetJobStatus(string jobId)
        {
            var job = _queueService.GetJobStatus(jobId);
            if (job == null) return NotFound(new { error = "Sistem Hatası: Belirtilen JobId bulunamadı." });

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