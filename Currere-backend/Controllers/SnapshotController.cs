using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    /// <summary>
    /// Çalışma Alanı Versiyonlama ve Zaman Makinesi İşlemleri
    /// </summary>
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class SnapshotController : ControllerBase
    {
        private readonly IWorkspaceSnapshotService _snapshotService;

        public SnapshotController(IWorkspaceSnapshotService snapshotService)
        {
            _snapshotService = snapshotService;
        }

        /// <summary>
        /// O anki çalışma alanının tam yedeğini alır.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateSnapshot(int workspaceId, [FromBody] CreateSnapshotRequest request)
        {
            try
            {
                var snapshot = await _snapshotService.CreateSnapshotAsync(workspaceId, request.Description);
                return Ok(new { message = "Yedek başarıyla alındı.", snapshotId = snapshot.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Çalışma alanını belirtilen yedeğe (zamanda geriye) döndürür.
        /// </summary>
        [HttpPost("restore/{snapshotId}")]
        public async Task<IActionResult> RestoreSnapshot(int workspaceId, int snapshotId)
        {
            try
            {
                await _snapshotService.RestoreSnapshotAsync(workspaceId, snapshotId);
                return Ok(new { message = "Çalışma alanı başarıyla geçmişteki haline döndürüldü!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Çalışma alanının geçmiş yedeklerini listeler.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(int workspaceId)
        {
            var history = await _snapshotService.GetSnapshotsAsync(workspaceId);
            return Ok(history);
        }
    }

}