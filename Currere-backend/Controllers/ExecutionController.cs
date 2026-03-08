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

        [HttpPost("run")]
        public async Task<IActionResult> RunCode([FromBody] ExecuteCodeDto request)
        {
            var result = await _executionService.ExecutePythonCodeAsync(request.Code);

            // Eğer hata varsa 400 fakat format => ExecutionResultDto
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}