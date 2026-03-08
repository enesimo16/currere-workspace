using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface IWorkspaceService
    {
        Task<WorkspaceResponseDto> CreateWorkspaceAsync(int userId, CreateWorkspaceDto dto);
        Task<List<WorkspaceResponseDto>> GetUserWorkspacesAsync(int userId);
        Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(int workspaceId, int userId);
        Task<bool> UpdateWorkspaceAsync(int workspaceId, int userId, UpdateWorkspaceDto dto);
        Task<bool> DeleteWorkspaceAsync(int workspaceId, int userId);
    }
}