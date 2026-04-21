using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace Currere_backend.Hubs
{
    public class SyncHub : Hub
    {
        private readonly IMemoryCache _cache;

        public SyncHub(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task JoinWorkspace(string token)
        {
            // Token'ı cache'den kontrol et
            if (_cache.TryGetValue(token, out int workspaceId))
            {
                await JoinWorkspaceById(workspaceId);
                await Clients.Caller.SendAsync("JoinedSuccess", workspaceId);
                Console.WriteLine($"[SyncHub] Token validated. Client (CLI) joined group: workspace-{workspaceId}");
            }
            else
            {
                await Clients.Caller.SendAsync("JoinFailed", "Geçersiz veya süresi dolmuş token.");
            }
        }

        public async Task JoinWorkspaceById(int workspaceId)
        {
            var groupName = $"workspace-{workspaceId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SyncHub] Client (Web) joined group: {groupName}");
        }

        public async Task SendCodeUpdate(int workspaceId, string fileName, string content)
        {
            var groupName = $"workspace-{workspaceId}";
            // Gönderen hariç gruptaki herkese ilet
            await Clients.OthersInGroup(groupName).SendAsync("ReceiveCodeUpdate", fileName, content);
        }
    }
}
