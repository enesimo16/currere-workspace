namespace Currere_backend.Services
{
    public interface IHuggingFaceService
    {
        // Modeli ve arayüzü huggingfaceden halledicek
        // model url'sini bize vericek
        Task<string> DeployModelToSpaceAsync(int userId, int workspaceId, string spaceName, string modelFileName);
    }
}