using Currere_backend.DTOs;
using Currere_backend.Services;
using FluentValidation; // YENİ: Kütüphaneyi ekledik
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

        public ExecutionController(
            ICodeExecutionService executionService,
            IDatasetProfilerService profilerService)
        {
            _executionService = executionService;
            _profilerService = profilerService;
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

            // workspaceId servise e gitti
            var result = await _executionService.ExecutePythonCodeAsync(workspaceId, request.Code);

            // Eğer hata varsa 400 fakat format => ExecutionResultDto
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
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