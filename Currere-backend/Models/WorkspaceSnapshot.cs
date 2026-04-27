using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Currere_backend.Models
{
    public class WorkspaceSnapshot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WorkspaceId { get; set; }

        [ForeignKey("WorkspaceId")]
        public Workspace Workspace { get; set; } = null!;

        /// <summary>
        /// Kullanıcının verdiği etiket (Örn: "Veri Temizliği Öncesi")
        /// </summary>
        public string Label { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty; 
        public string ZipFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Zip dosyasının byte cinsinden boyutu
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Zip içindeki dosya sayısı
        /// </summary>
        public int FileCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}