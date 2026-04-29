using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Currere_backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize] 
    [Route("api/[controller]")]
    [ApiController]
    public class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly AppDbContext _context;

        public WorkspaceController(IWorkspaceService workspaceService, AppDbContext context)
        {
            _workspaceService = workspaceService;
            _context = context;
        }

        // JWT 'den kullanici alma
        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceDto dto)
        {
            var userId = GetUserId();
            var result = await _workspaceService.CreateWorkspaceAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyWorkspaces()
        {
            var userId = GetUserId();
            var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId);
            return Ok(workspaces);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetWorkspace(int id)
        {
            var userId = GetUserId();
            var workspace = await _workspaceService.GetWorkspaceByIdAsync(id, userId);

            if (workspace == null) return NotFound("Proje bulunamadı veya erişim yetkiniz yok.");

            return Ok(workspace);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkspace(int id, [FromBody] UpdateWorkspaceDto dto)
        {
            var userId = GetUserId();
            var success = await _workspaceService.UpdateWorkspaceAsync(id, userId, dto);

            if (!success) return NotFound("Güncellenecek proje bulunamadı.");

            return Ok("Proje başarıyla güncellendi.");
        }

        [HttpPut("{id}/code")]
        public async Task<IActionResult> SaveWorkspaceCode(int id, [FromBody] SaveCodeDto dto)
        {
            var userId = GetUserId();
            var success = await _workspaceService.UpdateWorkspaceCodeAsync(id, userId, dto.Code);

            if (!success) return NotFound("Proje bulunamadı.");

            return Ok("Kod başarıyla kaydedildi.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkspace(int id)
        {
            var userId = GetUserId();
            var success = await _workspaceService.DeleteWorkspaceAsync(id, userId);

            if (!success) return NotFound("Silinecek proje bulunamadı.");

            return Ok("Proje başarıyla silindi.");
        }

        [HttpGet("{workspaceId}/export")]
        public async Task<IActionResult> ExportWorkspace(int workspaceId)
        {
            try
            {
                var userId = GetUserId();
                var workspace = await _workspaceService.GetWorkspaceByIdAsync(workspaceId, userId);

                if (workspace == null)
                    return NotFound(new { error = "Proje bulunamadı veya erişim yetkiniz yok." });

                var files = await _context.WorkspaceFiles
                    .Where(f => f.WorkspaceId == workspaceId)
                    .ToListAsync();

                var memoryStream = new MemoryStream();

                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        if (System.IO.File.Exists(file.FilePath))
                        {
                            var entry = archive.CreateEntry(file.FileName, CompressionLevel.Optimal);
                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                await fileStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }

                memoryStream.Position = 0;
                return File(memoryStream, "application/zip", $"workspace_{workspaceId}_export.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Dışa aktarma sırasında bir hata oluştu: {ex.Message}" });
            }
        }
    }
}