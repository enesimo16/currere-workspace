using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    /// <summary>
    /// Jupyter Stateful Kernel Yönetimi
    /// Hücreler arası değişken persistansı sağlayan long-lived kernel endpoint'leri.
    /// </summary>
    [Authorize]
    [Route("api/kernel/{workspaceId}")]
    [ApiController]
    public class KernelController : ControllerBase
    {
        private readonly KernelManagerService _kernelManager;
        private readonly ILogger<KernelController> _logger;

        public KernelController(KernelManagerService kernelManager, ILogger<KernelController> logger)
        {
            _kernelManager = kernelManager;
            _logger = logger;
        }

        /// <summary>
        /// Bir notebook hücresini stateful kernel üzerinde çalıştırır.
        /// Hücreler arası değişkenler korunur (shared_globals).
        /// </summary>
        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCell(int workspaceId, [FromBody] KernelExecuteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "Çalıştırılacak kod boş olamaz." });

            try
            {
                _logger.LogInformation("[KernelAPI] Execute — WorkspaceId: {WsId}, CodeLen: {Len}", workspaceId, request.Code.Length);

                var result = await _kernelManager.ExecuteCellAsync(workspaceId, request.Code);

                return Ok(new
                {
                    success = result.Success,
                    output = result.Success ? result.Message : "",
                    error = !result.Success ? result.Message : "",
                    errorType = result.ErrorType ?? ""
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KernelAPI] Execute HATA — WorkspaceId: {WsId}", workspaceId);
                return StatusCode(500, new { error = $"Kernel hatası: {ex.Message}" });
            }
        }

        /// <summary>
        /// Kernel'ı yeniden başlatır (hafızayı sıfırlar, tüm değişkenler kaybolur).
        /// </summary>
        [HttpPost("restart")]
        public async Task<IActionResult> RestartKernel(int workspaceId)
        {
            try
            {
                _logger.LogWarning("[KernelAPI] Restart — WorkspaceId: {WsId}", workspaceId);
                await _kernelManager.RestartKernelAsync(workspaceId);
                return Ok(new { message = "Kernel başarıyla yeniden başlatıldı. Tüm değişkenler sıfırlandı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KernelAPI] Restart HATA — WorkspaceId: {WsId}", workspaceId);
                return StatusCode(500, new { error = $"Kernel yeniden başlatılamadı: {ex.Message}" });
            }
        }

        /// <summary>
        /// Kernel'ın aktif olup olmadığını kontrol eder.
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetKernelStatus(int workspaceId)
        {
            var isAlive = _kernelManager.IsKernelAlive(workspaceId);
            return Ok(new { workspaceId, isAlive });
        }
    }

    /// <summary>
    /// Hücre çalıştırma isteği DTO'su
    /// </summary>
    public class KernelExecuteRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
