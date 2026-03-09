using Microsoft.AspNetCore.Http;
using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface IFileService
    {
        // Kullanıcı idsini güvenlik ile aliyoruz cünkü kendi workspaceyine atacagiz
        Task<FileUploadResponseDto> UploadFileAsync(int workspaceId, int userId, IFormFile file);
    }
}