using System.Security.Claims;
using Currere_backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Hubs
{
    [Authorize]
    public class SyncHub : Hub
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SyncHub> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public SyncHub(IMemoryCache cache, ILogger<SyncHub> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _cache = cache;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Yeni bir istemci bağlandığında çağrılır. Bağlantı sayacı ve loglama.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("[SyncHub] Yeni istemci bağlandı: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// İstemci bağlantısı koptuğunda çağrılır. Grup temizliği otomatiktir (SignalR bunu halleder)
        /// ancak burada loglama ve ek temizlik yapıyoruz.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "[SyncHub] İstemci anormal şekilde ayrıldı: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("[SyncHub] İstemci ayrıldı: {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// CLI/Extension token ile workspace'e katılma
        /// </summary>
        public async Task JoinWorkspace(string token)
        {
            // Token'ı cache'den kontrol et
            if (_cache.TryGetValue(token, out int workspaceId))
            {
                await JoinWorkspaceById(workspaceId);
                await Clients.Caller.SendAsync("JoinedSuccess", workspaceId);
                _logger.LogInformation("[SyncHub] Token doğrulandı. İstemci (CLI) gruba katıldı: workspace-{WorkspaceId}, Connection: {ConnectionId}", workspaceId, Context.ConnectionId);
            }
            else
            {
                await Clients.Caller.SendAsync("JoinFailed", "Geçersiz veya süresi dolmuş token.");
                _logger.LogWarning("[SyncHub] Geçersiz token ile katılma denemesi. Connection: {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Web frontend doğrudan workspace ID ile gruba katılma.
        /// JWT'den kullanıcı kimliği alınarak workspace sahipliği DB'den doğrulanır.
        /// </summary>
        public async Task JoinWorkspaceById(int workspaceId)
        {
            // JWT'den userId al
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new HubException("Yetkilendirme hatası: Kullanıcı kimliği alınamadı.");
            }

            // DB'den workspace ownership kontrol et
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ownsWorkspace = await db.Workspaces.AnyAsync(w => w.Id == workspaceId && w.UserId == userId);

            if (!ownsWorkspace)
            {
                _logger.LogWarning("[SyncHub] Yetkisiz workspace erişim denemesi! UserId: {UserId}, WorkspaceId: {WsId}, Connection: {ConnId}",
                    userId, workspaceId, Context.ConnectionId);
                throw new HubException("Bu workspace'e erişim yetkiniz yok.");
            }

            var groupName = $"workspace-{workspaceId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("[SyncHub] İstemci (Web) gruba katıldı: {GroupName}, Connection: {ConnectionId}", groupName, Context.ConnectionId);
        }

        /// <summary>
        /// Kod güncellemesini gruptaki diğer istemcilere ilet
        /// </summary>
        public async Task SendCodeUpdate(int workspaceId, string fileName, string content)
        {
            var groupName = $"workspace-{workspaceId}";
            // Gönderen hariç gruptaki herkese ilet
            await Clients.OthersInGroup(groupName).SendAsync("ReceiveCodeUpdate", fileName, content);
        }
    }
}
