using System.Security.Claims;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class HuggingFaceController : ControllerBase
    {
        private readonly IHuggingFaceService _huggingFaceService;

        public HuggingFaceController(IHuggingFaceService huggingFaceService)
        {
            _huggingFaceService = huggingFaceService;
        }

        /// <summary>
        /// Workspace'teki eğitilmiş bir modeli, otonom web arayüzü üreterek HuggingFace'e deploy eder.
        /// </summary>
        [HttpPost("deploy")]
        public async Task<IActionResult> DeployModel(int workspaceId, [FromBody] DeployRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var spaceUrl = await _huggingFaceService.DeployModelToSpaceAsync(
                    userId, workspaceId, request.SpaceName, request.ModelFileName);

                return Ok(new
                {
                    message = "Model başarıyla HuggingFace'e fırlatıldı! Space şu an inşa ediliyor (Building).",
                    url = spaceUrl
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class DeployRequest
    {
        public string SpaceName { get; set; } = string.Empty; // Örn: Currere-enes-detector
        public string ModelFileName { get; set; } = string.Empty; // Örn: model.pkl
    }
}S