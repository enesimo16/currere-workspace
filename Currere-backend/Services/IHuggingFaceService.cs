using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface IHuggingFaceService
    {
        // Modeli ve arayüzü huggingfaceden halledicek
        // model url'sini bize vericek
        Task<string> DeployModelToSpaceAsync(int userId, int workspaceId, string spaceName, string modelFileName);
        Task<string> GenerateSyntheticDataAsync(string prompt, int rowCount);
        Task<string> PushToHubAsync(int userId, int workspaceId, PushToHubDto dto);
    }
}