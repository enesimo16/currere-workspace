using Currere_backend.DTOs;
using Currere_backend.Models;

namespace Currere_backend.Services
{
    public interface ISyntheticDataService
    {
        Task<WorkspaceFile> GenerateDataAsync(int workspaceId, SyntheticDataRequest request);
    }
}