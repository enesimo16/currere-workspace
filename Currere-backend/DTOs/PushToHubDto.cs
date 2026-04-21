namespace Currere_backend.DTOs
{
    public class PushToHubDto
    {
        public string RepoName { get; set; } = string.Empty; // Örn: enesyel/my-model
        public bool IsPrivate { get; set; } = false;
        public string CommitMessage { get; set; } = "Otonom Currere AI tarafından aktarıldı";
        public string FileName { get; set; } = string.Empty;
        public string HfToken { get; set; } = string.Empty;
    }
}
