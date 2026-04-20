using System.Security.Claims;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    /// <summary>
    /// Çalışma Alanı (Workspace) Dosya Yönetimi İşlemleri
    /// </summary>
    // [Authorize] // UYARI: JWT token ile test ederken burayı açmayı unutma!
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>
        /// Çalışma alanına (Workspace) yeni bir dosya yükler.
        /// </summary>
        /// <remarks>
        /// Yüklenen veri dosyası (CSV, XLSX, JSON) güvenli bir şekilde sunucuya kaydedilir ve arka planda (Hangfire) otomatik olarak istatistiksel veri profili (röntgeni) çıkarılır. 
        /// Kod dosyaları (.ipynb, .py) ise profilleme yapılmadan sisteme alınır. Sistem kuralları gereği yüklenen tüm dosyalar 4 saat sonra otomatik olarak imha edilir.
        /// </remarks>
        /// <param name="workspaceId">Dosyanın yükleneceği aktif çalışma alanının ID'si.</param>
        /// <param name="file">Yüklenecek olan fiziksel dosya (Maksimum 50 MB, desteklenen formatlar: .csv, .xlsx, .json, .txt, .ipynb, .py).</param>
        /// <returns>Dosya kimliği (ID), geçerlilik süresi ve işlemin durumu.</returns>
        /// <response code="200">Dosya başarıyla yüklendi ve arka plan analiz işlemleri başlatıldı.</response>
        /// <response code="400">Geçersiz dosya formatı, boyutu aşımı veya sunucu hatası.</response>
        /// <response code="401">Yetkilendirme hatası (Geçerli bir Bearer Token sağlanmadı).</response>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadFile(int workspaceId, IFormFile file)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var result = await _fileService.UploadFileAsync(workspaceId, userId, file);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetWorkspaceFiles(int workspaceId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var files = await _fileService.GetWorkspaceFilesAsync(workspaceId, userId);
                return Ok(files);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}