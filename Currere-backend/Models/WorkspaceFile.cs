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

        /// <summary>
        /// true → Kaynak kod dosyası (.py, .ipynb, .js, .cs, .ts, .md vb.)
        ///         GC (FileCleanupService) tarafından ASLA silinmez.
        /// false → Geçici veri dosyası (.csv, .xlsx, .json, .log, .tmp vb.)
        ///          ExpiresAt dolduğunda otomatik silinir.
        /// </summary>
        public bool IsPermanent { get; set; } = false;

        public string? ProfileJson { get; set; } // py dan gelen jsonu db e kaydediyoruz

        public string? DomainContext { get; set; } // AI raporu baglam
        public Workspace Workspace { get; set; } = null!;
    }
}