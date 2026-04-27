using System.Security.Claims;
using Currere_backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Hubs
{
    [Authorize]
    public class TerminalHub : Hub
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TerminalHub> _logger;

        public TerminalHub(IServiceScopeFactory serviceScopeFactory, ILogger<TerminalHub> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        // logları dinleme servisleri
        public async Task JoinWorkspace(string workspaceId)
        {
            if (!int.TryParse(workspaceId, out var wsId))
            {
                throw new HubException("Geçersiz workspace ID.");
            }

            // JWT'den userId al
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new HubException("Yetkilendirme hatası: Kullanıcı kimliği alınamadı.");
            }

            // DB'den workspace ownership kontrol et
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ownsWorkspace = await db.Workspaces.AnyAsync(w => w.Id == wsId && w.UserId == userId);

            if (!ownsWorkspace)
            {
                _logger.LogWarning("[TerminalHub] Yetkisiz workspace erişim denemesi! UserId: {UserId}, WorkspaceId: {WsId}, Connection: {ConnId}",
                    userId, wsId, Context.ConnectionId);
                throw new HubException("Bu workspace'e erişim yetkiniz yok.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, workspaceId);
            await Clients.Caller.SendAsync("ReceiveLog", $"[Sistem] Çalışma alanı {workspaceId} terminaline başarıyla bağlanıldı.");
        }

        public async Task LeaveWorkspace(string workspaceId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, workspaceId);
            await Clients.Caller.SendAsync("ReceiveLog", $"[Sistem] Çalışma alanı {workspaceId} terminalinden çıkıldı.");
        }
    }
}