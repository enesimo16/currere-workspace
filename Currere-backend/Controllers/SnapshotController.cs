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
        [HttpPost]
        public async Task<IActionResult> CreateSnapshot(int workspaceId, [FromBody] CreateSnapshotRequest request)
        {
            try
            {
                // Eğer frontend UserId göndermediyse JWT'den alıyoruz (Güvenli yöntem)
                if (request.UserId <= 0)
                {
                    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int userId))
                    {
                        request.UserId = userId;
                    }
                }

                var label = !string.IsNullOrWhiteSpace(request.Label) ? request.Label : request.Description;
                var snapshot = await _snapshotService.CreateSnapshotAsync(workspaceId, label, request.Description);
                return Ok(new { message = "Yedek başarıyla alındı.", snapshotId = snapshot.Id, sizeKB = snapshot.SizeBytes / 1024, fileCount = snapshot.FileCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Çalışma alanını belirtilen yedeğe (zamanda geriye) döndürür.
        /// </summary>
        [HttpPost("{snapshotId}/restore")]
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
        [HttpGet]
        public async Task<IActionResult> GetHistory(int workspaceId)
        {
            var history = await _snapshotService.GetSnapshotsAsync(workspaceId);
            return Ok(history);
        }

        /// <summary>
        /// Belirtilen yedeği siler.
        /// </summary>
        [HttpDelete("{snapshotId}")]
        public async Task<IActionResult> DeleteSnapshot(int workspaceId, int snapshotId)
        {
            var result = await _snapshotService.DeleteSnapshotAsync(workspaceId, snapshotId);
            if (!result) return NotFound(new { error = "Yedek bulunamadı." });
            
            return Ok(new { message = "Yedek başarıyla silindi." });
        }
    }

}