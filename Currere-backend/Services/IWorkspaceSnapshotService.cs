using Currere_backend.Models;

namespace Currere_backend.Services
{
    public interface IWorkspaceSnapshotService
    {
        Task<WorkspaceSnapshot> CreateSnapshotAsync(int workspaceId, string description);
        Task<bool> RestoreSnapshotAsync(int workspaceId, int snapshotId);
        Task<bool> DeleteSnapshotAsync(int workspaceId, int snapshotId);
        Task<List<WorkspaceSnapshot>> GetSnapshotsAsync(int workspaceId);
    }
}