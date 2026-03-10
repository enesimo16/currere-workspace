namespace Currere_backend.Services
{
    public interface IDatasetProfilerService
    {
        Task<string> ProfileDatasetAsync(int workspaceId, string fileName);
    }
}