using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace")]
    [ApiController]
    public class SyncController : ControllerBase
    {
        private readonly IMemoryCache _cache;

        public SyncController(IMemoryCache cache)
        {
            _cache = cache;
        }

        [HttpGet("{id}/sync-token")]
        public IActionResult GetSyncToken(int id)
        {
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
