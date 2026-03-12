namespace Currere_backend.Models
{
    public class WorkspaceFile
    {
        public int Id { get; set; }
        public int WorkspaceId { get; set; }
        public string FileName { get; set; } = string.Empty; // Örn: data.csv
        public string FilePath { get; set; } = string.Empty; // sunucudaki path
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } // Yüklenilen dosyasının expire vakti

        public string? ProfileJson { get; set; } // py dan gelen jsonu db e kaydediyoruz

        public string? DomainContext { get; set; } // AI raporu baglam
        public Workspace Workspace { get; set; } = null!;
    }
}