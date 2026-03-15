namespace Currere_backend.Services
{
    public interface IProfileJobService
    {
        // Hangfire'ın arka planda tetikleyeceği metot
        Task GenerateAndSaveProfileAsync(int workspaceId, string uniqueFileName, int fileId);
    }
}