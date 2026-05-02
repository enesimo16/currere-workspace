using System.Security.Claims;
using Currere_backend.Data;
using Currere_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/user/settings")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
                    KaggleKey = dto.Key,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserIntegrations.Add(integration);
            }
            else
            {
                integration.KaggleUsername = dto.Username;
                integration.KaggleKey = dto.Key;
                integration.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kaggle ayarları başarıyla kaydedildi." });
        }
    }

    public class KaggleSettingsDto
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
    }
}
