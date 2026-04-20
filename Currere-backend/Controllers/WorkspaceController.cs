using System.Security.Claims;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    [Authorize] 
    [Route("api/[controller]")]
    [ApiController]
    public class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceController(IWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
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
    }
}