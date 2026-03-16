using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Currere_backend.Models
{
    public class UserIntegration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        // Şifrelenmiş olarak tutulacak token'lar
        public string? GithubToken { get; set; }
        public string? HuggingFaceToken { get; set; }
        public string? KaggleUsername { get; set; } // username acık
        public string? KaggleKey { get; set; }      // api key şifreli

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}