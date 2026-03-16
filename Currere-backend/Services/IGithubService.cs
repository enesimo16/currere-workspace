namespace Currere_backend.Services
{
    public interface IGithubService
    {
        // workspace için yeni repo
        Task<string> PushWorkspaceToGithubAsync(int userId, int workspaceId, string repoName, string commitMessage);
    }
}