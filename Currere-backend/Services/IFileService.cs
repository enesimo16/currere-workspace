using Microsoft.AspNetCore.Http;
using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface IFileService
    {
        // Kullanıcı idsini güvenlik ile aliyoruz cünkü kendi workspaceyine atacagiz
        Task<FileUploadResponseDto> UploadFileAsync(int workspaceId, int userId, IFormFile file);
        Task<List<WorkspaceFileDto>> GetWorkspaceFilesAsync(int workspaceId, int userId);
        Task<string> GetFileContentAsync(int workspaceId, int userId, string fileName);
        Task<bool> UpdateFileContentAsync(int workspaceId, int userId, string fileName, string content);
        Task<FileUploadResponseDto> CreateFileAsync(int workspaceId, int userId, string fileName);
        Task<bool> DeleteFileAsync(int workspaceId, int userId, string fileName);
        Task<bool> RenameFileAsync(int workspaceId, int userId, string oldFileName, string newFileName);
    }
}