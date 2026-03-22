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

        public string Description { get; set; } = string.Empty; 
        public string ZipFilePath { get; set; } = string.Empty; // scopy
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}