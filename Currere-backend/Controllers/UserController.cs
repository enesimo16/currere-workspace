using System.Security.Claims;
using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/user/settings")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public UserController(AppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpGet("kaggle")]
        public async Task<IActionResult> GetKaggleSettings()
        {
            var userId = GetUserId();

            var integration = await _context.UserIntegrations
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (integration == null || string.IsNullOrEmpty(integration.KaggleUsername))
            {
                return Ok(new { isConfigured = false, username = "" });
            }

            return Ok(new
            {
                isConfigured = !string.IsNullOrEmpty(integration.KaggleKey),
                username = integration.KaggleUsername
            });
        }

        [HttpPost("kaggle")]
        public async Task<IActionResult> SaveKaggleSettings([FromBody] KaggleSettingsDto dto)
        {
            var userId = GetUserId();

            var integration = await _context.UserIntegrations
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (integration == null)
            {
                integration = new UserIntegration
                {
                    UserId = userId,
                    KaggleUsername = dto.Username,
                    KaggleKey = _encryptionService.Encrypt(dto.Key),
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserIntegrations.Add(integration);
            }
            else
            {
                integration.KaggleUsername = dto.Username;
                integration.KaggleKey = _encryptionService.Encrypt(dto.Key);
                integration.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kaggle ayarları başarıyla kaydedildi." });
        }
    }
}
