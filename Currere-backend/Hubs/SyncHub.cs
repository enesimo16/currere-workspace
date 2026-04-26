using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Hubs
{
    public class SyncHub : Hub
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SyncHub> _logger;

        public SyncHub(IMemoryCache cache, ILogger<SyncHub> logger)
        {
            _cache = cache;
            _logger = logger;
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
        /// Web frontend doğrudan workspace ID ile gruba katılma
        /// </summary>
        public async Task JoinWorkspaceById(int workspaceId)
        {
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
