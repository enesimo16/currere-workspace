using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly AppDbContext _context;
        private readonly IDatasetProfilerService _profilerService; 

        public AiController(IAiService aiService, AppDbContext context, IDatasetProfilerService profilerService)
        {
            _aiService = aiService;
            _context = context;
            _profilerService = profilerService;
        }

        [HttpPost("generate-code")]
        public async Task<IActionResult> GenerateCode(int workspaceId, [FromBody] GenerateCodeRequestDto request)
        {
            var file = await _context.WorkspaceFiles
                .FirstOrDefaultAsync(f => f.Id == request.FileId && f.WorkspaceId == workspaceId);

            if (file == null)
                return NotFound(new { error = "Dosya bulunamadı veya bu çalışma alanına ait değil." });

            var fileNameInWorkspace = Path.GetFileName(file.FilePath);

            if (string.IsNullOrEmpty(file.ProfileJson))
            {
                try
                {
                    // Anında profil
                    var newProfileJson = await _profilerService.ProfileDatasetAsync(workspaceId, fileNameInWorkspace);

                    // Veritabanı
                    file.ProfileJson = newProfileJson;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = $"Sistem veriyi okuyamadı. Dosya formatı bozuk olabilir. Hata: {ex.Message}" });
                }
            }

            try
            {
                var generatedCode = await _aiService.GeneratePythonCodeAsync(request.Prompt, file.ProfileJson, fileNameInWorkspace);

                return Ok(new
                {
                    message = "Yapay zeka kodu başarıyla üretti.",
                    code = generatedCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI Motoru Hatası: {ex.Message}" });
            }
        }
    }
}