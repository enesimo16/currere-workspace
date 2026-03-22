using Microsoft.AspNetCore.SignalR;

namespace Currere_backend.Hubs
{
    public class TerminalHub : Hub
    {
        // logları dinleme servisleri
        public async Task JoinWorkspace(string workspaceId)
        {
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