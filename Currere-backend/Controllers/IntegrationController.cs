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
    /// <summary>
    /// Üçüncü Parti API Entegrasyonları (Kaggle, GitHub, HuggingFace)
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class IntegrationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public IntegrationController(AppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        /// <summary>
        /// Kullanıcının Kaggle API bilgilerini şifreleyerek kaydeder.
        /// </summary>
        [HttpPost("kaggle")]
        public async Task<IActionResult> SaveKaggleIntegration([FromBody] KaggleIntegrationDto request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // integration bul
            var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);
            if (integration == null)
            {
                integration = new UserIntegration { UserId = userId };
                _context.UserIntegrations.Add(integration);
            }

            // Username düz metin, API Key ise AES-256 ile şifreli kaydetme
            integration.KaggleUsername = request.Username;
            integration.KaggleKey = _encryptionService.Encrypt(request.ApiKey);
            integration.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kaggle entegrasyonu güvenle kaydedildi." });
        }

        /// <summary>
        /// Kullanıcının mevcut entegrasyon durumlarını (şifreleri gizleyerek) döner.
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetIntegrationStatus()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);

            // frontende şifreyi degil baglı olup olmadıgını dönüyoruz
            return Ok(new
            {
                IsKaggleConnected = !string.IsNullOrEmpty(integration?.KaggleKey),
                KaggleUsername = integration?.KaggleUsername,
                IsGithubConnected = !string.IsNullOrEmpty(integration?.GithubToken),
                IsHuggingFaceConnected = !string.IsNullOrEmpty(integration?.HuggingFaceToken),
                LastUpdated = integration?.UpdatedAt
            });
        }
    }

}