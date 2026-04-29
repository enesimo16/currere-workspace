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
    [Authorize] // UYARI: JWT token ile test ederken burayı açmayı unutma!
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
        [HttpGet("{fileName}/raw")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetFileRawContent(int workspaceId, string fileName)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var content = await _fileService.GetFileContentAsync(workspaceId, userId, fileName);
                return Ok(new { content });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{fileName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateFileContent([FromRoute] int workspaceId, [FromRoute] string fileName, [FromBody] UpdateFileContentDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "İçerik gönderilmedi." });

                var content = request.Content;

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var success = await _fileService.UpdateFileContentAsync(workspaceId, userId, fileName, content);
                if (!success) return BadRequest(new { error = "Dosya güncellenemedi." });
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateFile(int workspaceId, [FromBody] CreateFileRequest request)
        {
            try
            {
                // ═══ Path Traversal koruması — controller katmanında erken reddet ═══
                if (string.IsNullOrWhiteSpace(request.FileName))
                    return BadRequest(new { error = "Dosya adı boş olamaz." });

                var cleanName = Path.GetFileName(request.FileName);
                if (cleanName != request.FileName || 
                    request.FileName.Contains("..") || 
                    request.FileName.Contains('/') || 
                    request.FileName.Contains('\\'))
                {
                    return BadRequest(new { error = "Geçersiz dosya adı: Dizin atlama girişimi tespit edildi." });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await _fileService.CreateFileAsync(workspaceId, userId, request.FileName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{fileName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteFile(int workspaceId, string fileName)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var success = await _fileService.DeleteFileAsync(workspaceId, userId, fileName);
                if (!success) return BadRequest(new { error = "Dosya silinemedi." });
                return Ok(new { message = "Dosya başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{fileName}/rename")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RenameFile(int workspaceId, string fileName, [FromBody] RenameFileRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var success = await _fileService.RenameFileAsync(workspaceId, userId, fileName, request.NewFileName);
                if (!success) return BadRequest(new { error = "Dosya adı güncellenemedi." });
                return Ok(new { message = "Dosya başarıyla yeniden adlandırıldı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class UpdateFileContentDto
    {
        public string Content { get; set; } = string.Empty;
    }

    public class CreateFileRequest
    {
        public string FileName { get; set; } = string.Empty;
    }

    public class RenameFileRequest
    {
        public string NewFileName { get; set; } = string.Empty;
    }
}