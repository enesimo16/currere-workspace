using System.Security.Claims;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class KaggleController : ControllerBase
    {
        private readonly IKaggleService _kaggleService;
        private readonly AppDbContext _context;
        private readonly ILogger<KaggleController> _logger;

        public KaggleController(IKaggleService kaggleService, AppDbContext context, ILogger<KaggleController> logger)
        {
            _kaggleService = kaggleService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchDatasets([FromQuery] string query)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);
                if (integration == null || string.IsNullOrEmpty(integration.KaggleUsername) || string.IsNullOrEmpty(integration.KaggleKey))
                {
                    _logger.LogError("Kaggle credentials not found for user {UserId}", userId);
                    return BadRequest(new { error = "Kaggle entegrasyonu bulunamadı. Lütfen önce API Key'inizi kaydedin." });
                }

                var rawJson = await _kaggleService.SearchDatasetsAsync(userId, query);

                // Kaggle'dan dönen veriyi direkt frontend'e iletiyoruz
                return Content(rawJson, "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("download")]
        public async Task<IActionResult> DownloadDataset(int workspaceId, [FromBody] KaggleDownloadRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // request.DatasetRef -> Örn: "heptapod/studetnt"
                var extractedFiles = await _kaggleService.DownloadDatasetAsync(userId, workspaceId, request.DatasetRef);

                return Ok(new
                {
                    message = "Kaggle veri seti başarıyla indirildi ve çalışma alanına çıkartıldı.",
                    files = extractedFiles
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

}