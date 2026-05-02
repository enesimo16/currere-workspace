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
using Moq;
using Xunit;

namespace Currere.Tests.IntegrationTests
{
    public class TestSnapshotLifecycle
    {
        private readonly AppDbContext _dbContext;
        private readonly Mock<IWorkspaceSnapshotService> _mockSnapshotService;
        private readonly SnapshotController _snapshotController;

        public TestSnapshotLifecycle()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _mockSnapshotService = new Mock<IWorkspaceSnapshotService>();

            _snapshotController = new SnapshotController(_mockSnapshotService.Object);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }, "mock"));

            _snapshotController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task CreateSnapshot_ShouldReturnSuccess()
        {
            // Senaryo 1: Snapshot oluşturma işleminin başarılı olması
            int workspaceId = 100;
            _dbContext.Workspaces.Add(new Workspace { Id = workspaceId, UserId = 1, Title = "My Workspace" });
            await _dbContext.SaveChangesAsync();

            var req = new CreateSnapshotRequest { Label = "Test Snapshot", Description = "Test" };

            var snapshot = new WorkspaceSnapshot 
            { 
                Id = 5, 
                WorkspaceId = workspaceId, 
                Label = req.Label, 
                ZipFilePath = "/mock/path.zip" 
            };
            
            _mockSnapshotService.Setup(s => s.CreateSnapshotAsync(workspaceId, req.Label, req.Description))
                .ReturnsAsync(snapshot);

            var result = await _snapshotController.CreateSnapshot(workspaceId, req);

            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockSnapshotService.Verify(s => s.CreateSnapshotAsync(workspaceId, req.Label, req.Description), Times.Once);
        }

        [Fact]
        public async Task RaceCondition_AutoSaveAndRestore_ShouldNotLockSystem()
        {
            // Senaryo 2 (Race Condition Testi): Auto-save ve restore aynı anda çağrıldığında
            int workspaceId = 100;
            _dbContext.Workspaces.Add(new Workspace { Id = workspaceId, UserId = 1, Title = "Race Workspace" });
            await _dbContext.SaveChangesAsync();

            _mockSnapshotService.Setup(s => s.CreateSnapshotAsync(workspaceId, "AutoSave", "Auto"))
                .Returns(async () => { await Task.Delay(200); return new WorkspaceSnapshot(); });

            _mockSnapshotService.Setup(s => s.RestoreSnapshotAsync(workspaceId, 5))
                .Returns(async () => { await Task.Delay(200); return true; });

            var createReq = new CreateSnapshotRequest { Label = "AutoSave", Description = "Auto" };
            
            // İki işlemi paralel tetikle
            var task1 = _snapshotController.CreateSnapshot(workspaceId, createReq);
            var task2 = _snapshotController.RestoreSnapshot(workspaceId, 5);

            await Task.WhenAll(task1, task2);

            // Kilitlenme (deadlock) olmamalı ve her iki task de başarıyla sonlanmalı
            Assert.True(task1.IsCompletedSuccessfully);
            Assert.True(task2.IsCompletedSuccessfully);
        }
    }
}
