using Currere_backend.DTOs;
using Currere_backend.Services;
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

        public ExecutionController(ICodeExecutionService executionService)
        {
            _executionService = executionService;
        }

        [HttpPost("{workspaceId}/run")]
        public async Task<IActionResult> RunCode(int workspaceId, [FromBody] ExecuteCodeDto request)
        {
            // workspaceId servise e gitti
            var result = await _executionService.ExecutePythonCodeAsync(workspaceId, request.Code);

            // Eğer hata varsa 400 fakat format => ExecutionResultDto
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}