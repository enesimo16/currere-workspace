using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Currere_backend.Controllers;
using Currere_backend.Data;
using Currere_backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Currere.Tests.IntegrationTests
{
    public class TestVSCodeSync
    {
        private readonly IMemoryCache _cache;
        private readonly SyncController _syncController;

        public TestVSCodeSync()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            _cache = serviceProvider.GetRequiredService<IMemoryCache>();

            _syncController = new SyncController(_cache);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }, "mock"));

            _syncController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public void GenerateToken_ShouldCreateCorrectFormatAndExpiration()
        {
            // Senaryo 1: Token'ın doğru formatta ve doğru geçerlilik süresiyle üretilmesi
            int workspaceId = 55;

            var result = _syncController.GetSyncToken(workspaceId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var val = okResult.Value;
            var tokenProp = val?.GetType().GetProperty("token");
            var expProp = val?.GetType().GetProperty("expiresAt");

            Assert.NotNull(tokenProp);
            Assert.NotNull(expProp);

            string token = (string)tokenProp.GetValue(val);
            DateTime expiresAt = (DateTime)expProp.GetValue(val);

            Assert.False(string.IsNullOrEmpty(token));
            Assert.True(expiresAt > DateTime.UtcNow); // Token süresi gelecekte olmalı
            
            // Cache'e kayıt işlemi doğrulandı mı?
            var cachedWorkspaceId = _cache.Get<int>(token);
            Assert.Equal(workspaceId, cachedWorkspaceId);
        }
    }
}
