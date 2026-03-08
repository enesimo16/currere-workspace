using System.ComponentModel.DataAnnotations;

namespace Currere_backend.DTOs
{

    public enum WorkspaceFormat
    {
        Python = 1,     // .py
        Notebook = 2    // .ipynb
    }

    public enum RuntimeType
    {
        CPU = 1,
        GPU = 2       // suan sadece cpu kýsmý var ama gpu eklentisine hazýr olmalýyýz
    }

    public enum MessageRole
    {
        System = 1,   // ai'ya iþlenecek backend claims
        User = 2,     // user rquest
        AI = 3        // ai response
    }


    public class RegisterDto
    {
        [Required(ErrorMessage = "Ad alaný zorunludur.")]
        [MinLength(2, ErrorMessage = "Ad en az 2 karakter olmalýdýr.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alaný zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email alaný zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Þifre alaný zorunludur.")]
        [MinLength(6, ErrorMessage = "Þifre en az 6 karakter olmalýdýr.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Email alaný zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Þifre zorunludur.")]
        public string Password { get; set; } = string.Empty;
    }

    public class CreateWorkspaceDto
    {
        [Required(ErrorMessage = "Proje adý zorunludur.")]
        [MaxLength(100, ErrorMessage = "Proje adý çok uzun.")]
        public string Title { get; set; } = "Untitled Workspace";

        public WorkspaceFormat Format { get; set; } = WorkspaceFormat.Python;
        public RuntimeType Runtime { get; set; } = RuntimeType.CPU;
    }

    public class UpdateWorkspaceDto
    {
        public string Title { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty; // Kodun o anki hali
    }

    public class WorkspaceResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Runtime { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}