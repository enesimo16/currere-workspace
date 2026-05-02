using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Currere_backend.Controllers;
using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Currere.Tests.IntegrationTests
{
    public class TestKaggleIntegration
    {
        private readonly AppDbContext _dbContext;
        private readonly Mock<IEncryptionService> _mockEncryptionService;
        private readonly UserController _userController;

        public TestKaggleIntegration()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _mockEncryptionService = new Mock<IEncryptionService>();

            _userController = new UserController(_dbContext, _mockEncryptionService.Object);

            // Mock User context
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }, "mock"));

            _userController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task SaveKaggleSettings_ShouldEncryptKeyAndSave()
        {
            // Senaryo 1: POST çağrıldığında şifreli key kaydediliyor mu?
            var dto = new KaggleSettingsDto { Username = "testuser", Key = "plain_api_key" };
            _mockEncryptionService.Setup(s => s.Encrypt("plain_api_key")).Returns("encrypted_api_key");

            var result = await _userController.SaveKaggleSettings(dto);

            Assert.IsType<OkObjectResult>(result);
            var integration = await _dbContext.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == 1);
            Assert.NotNull(integration);
            Assert.Equal("testuser", integration.KaggleUsername);
            Assert.Equal("encrypted_api_key", integration.KaggleKey);
            _mockEncryptionService.Verify(s => s.Encrypt("plain_api_key"), Times.Once);
        }

        [Fact]
        public async Task GetKaggleSettings_ShouldReturnUsernameButNoKey()
        {
            // Senaryo 2: GET çağrıldığında username gelmeli ama API Key GİZLİ olmalı
            _dbContext.UserIntegrations.Add(new UserIntegration
            {
                UserId = 1,
                KaggleUsername = "testuser",
                KaggleKey = "encrypted_api_key",
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            var result = await _userController.GetKaggleSettings();
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var value = okResult.Value;
            var isConfiguredProperty = value?.GetType().GetProperty("isConfigured");
            var usernameProperty = value?.GetType().GetProperty("username");
            var keyProperty = value?.GetType().GetProperty("key"); // Should be null

            Assert.NotNull(isConfiguredProperty);
            Assert.NotNull(usernameProperty);
            Assert.Null(keyProperty); // Key asla dönülmemeli!

            bool isConfigured = (bool)isConfiguredProperty.GetValue(value);
            string username = (string)usernameProperty.GetValue(value);

            Assert.True(isConfigured);
            Assert.Equal("testuser", username);
        }

        [Fact]
        public async Task SearchDatasets_ShouldReturn400_WhenCredentialsMissing()
        {
            // Senaryo 3: Kimlik bilgileri yokken 400 Bad Request dönmeli
            var mockKaggleService = new Mock<IKaggleService>();
            var mockLogger = new Mock<ILogger<KaggleController>>();
            
            var kaggleController = new KaggleController(mockKaggleService.Object, _dbContext, mockLogger.Object)
            {
                ControllerContext = _userController.ControllerContext // Aynı user (1)
            };

            // DB'de integration yok!

            var result = await kaggleController.SearchDatasets("dogs");

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequestResult.Value;
            var errorProp = value?.GetType().GetProperty("error");
            
            Assert.NotNull(errorProp);
            var errorMsg = (string)errorProp.GetValue(value);
            Assert.Contains("Kaggle entegrasyonu bulunamadı", errorMsg);
        }
    }
}
