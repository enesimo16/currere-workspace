using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class SyntheticDataController : ControllerBase
    {
        private readonly ISyntheticDataService _syntheticDataService;

        public SyntheticDataController(ISyntheticDataService syntheticDataService)
        {
            _syntheticDataService = syntheticDataService;
        }

        /// <summary>
        /// Belirlenen modda otonom sentetik veri üretir ve çalışma alanına kaydeder.
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(int workspaceId, [FromBody] SyntheticDataRequest request)
        {
            try
            {
                var newFile = await _syntheticDataService.GenerateDataAsync(workspaceId, request);

                return Ok(new
                {
                    message = $"{request.Mode} modunda sentetik veri başarıyla üretildi!",
                    fileId = newFile.Id,
                    fileName = newFile.FileName
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}