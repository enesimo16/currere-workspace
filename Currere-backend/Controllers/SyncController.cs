using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Currere_backend.Data;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace")]
    [ApiController]
    public class SyncController : ControllerBase
    {
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _context;

        public SyncController(IMemoryCache cache, AppDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        [HttpGet("{id}/sync-token")]
        public async Task<IActionResult> GetSyncToken(int id)
        {
            // O-4 Fix: Workspace sahipliği (ownership) kontrolü eklendi.
            // Eski kod: [Authorize] vardı ama herhangi bir yetkili kullanıcı
            // başkasının workspace ID'siyle token üretebiliyordu (IDOR açığı).
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var currentUserId))
                return Unauthorized(new { error = "Kimlik doğrulama başarısız." });

            var ownsWorkspace = await _context.Workspaces
                .AnyAsync(w => w.Id == id && w.UserId == currentUserId);

            if (!ownsWorkspace)
                return Forbid(); // 403 — başkasının workspace'i

            // Token üret (GUID)
            var token = Guid.NewGuid().ToString("N").Substring(0, 16);

            // Cache'e kaydet (10 dakika)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };

            _cache.Set(token, id, cacheOptions);

            return Ok(new { token = token, expiresAt = DateTime.UtcNow.AddMinutes(10) });
        }
    }
}

