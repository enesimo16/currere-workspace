namespace Currere_backend.Models
{
    public class WorkspaceSecret
    {
        public int Id { get; set; }
        public int WorkspaceId { get; set; }
        public string KeyName { get; set; } = string.Empty; // Örn: KAGGLE_KEY
        public string SecretValue { get; set; } = string.Empty; // Şifreli

        // İlişki NP
        public Workspace Workspace { get; set; } = null!;
    }
}