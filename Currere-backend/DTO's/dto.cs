using System.ComponentModel.DataAnnotations;

namespace Currere_backend.DTOs
{

    // ENUM'S

    public enum WorkspaceFormat
    {
        Python = 1,     // .py
        Notebook = 2    // .ipynb
    }

    public enum RuntimeType
    {
        CPU = 1,
        GPU = 2       // suan sadece cpu ksm var ama gpu eklentisine hazr olmalyz
    }

    public enum MessageRole
    {
        System = 1,   // ai'ya ilenecek backend claims
        User = 2,     // user rquest
        AI = 3        // ai response
    }

    public enum GenerationMode
    {
        FastAndFake = 1,
        ZeroShotRealistic = 2,
        DigitalTwin = 3
    }


    // AUTH SYSTEM


    public class RegisterDto
    {
        [Required(ErrorMessage = "Ad alan zorunludur.")]
        [MinLength(2, ErrorMessage = "Ad en az 2 karakter olmaldr.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alan zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email alan zorunludur.")]
        [EmailAddress(ErrorMessage = "Geerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "ifre alan zorunludur.")]
        [MinLength(6, ErrorMessage = "ifre en az 6 karakter olmaldr.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Email alan zorunludur.")]
        [EmailAddress(ErrorMessage = "Geerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "ifre zorunludur.")]
        public string Password { get; set; } = string.Empty;
    }

    // STUDENT PACK

    public class LinkStudentEmailDto
    {
        [Required(ErrorMessage = "renci e-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geerli bir e-posta format giriniz.")]
        public string StudentEmail { get; set; } = string.Empty;
    }

    // WORKSPACE ROOM

    public class CreateWorkspaceDto
    {
        [Required(ErrorMessage = "Proje ad zorunludur.")]
        [MaxLength(100, ErrorMessage = "Proje ad ok uzun.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_]+$", ErrorMessage = "Proje adı sadece harf, rakam, tire, alt çizgi ve boşluk içerebilir.")]
        public string Title { get; set; } = "Untitled Workspace";

        public WorkspaceFormat Format { get; set; } = WorkspaceFormat.Python;
        public RuntimeType Runtime { get; set; } = RuntimeType.CPU;
    }

    public class UpdateWorkspaceDto
    {
        [MaxLength(100, ErrorMessage = "Proje adı çok uzun.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_]*$", ErrorMessage = "Proje adı sadece harf, rakam, tire, alt çizgi ve boşluk içerebilir.")]
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


    // DOCKERIZATION

    public class ExecuteCodeDto
    {
        [Required(ErrorMessage = "altrlacak kod bo olamaz.")]
        public string Code { get; set; } = string.Empty;
        public string? DatasetFileName { get; set; } = string.Empty;
    }

    public class ExecutionResultDto
    {
        public string Output { get; set; } = string.Empty;   
        public string Error { get; set; } = string.Empty;    
        public bool IsSuccess { get; set; }                 
        public long ExecutionTimeMs { get; set; }
        public List<string> ArtifactUrls { get; set; } = new List<string>();

        public string ErrorType { get; set; } = string.Empty; // runnerdan error kodu almak cini
    }


    // FILE SERVICE

    public class FileUploadResponseDto
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; } // usera dosya silinme sresini gsterme
        public string Message { get; set; } = string.Empty;
    }

    // AI INTEGRATION

    public class GenerateCodeRequestDto
    {
        public int FileId { get; set; }

        public string Prompt { get; set; } = string.Empty;
    }

    // api integration
    public class KaggleIntegrationDto
    {
        public string Username { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    // kaggle 

    public class KaggleDownloadRequest
    {
        public string DatasetRef { get; set; } = string.Empty; // Kaggle format (kullanc/dataset-ad)
    }

    // github

    public class GithubPushRequest
    {
        public string RepoName { get; set; } = string.Empty;
        public string? CommitMessage { get; set; }
    }

    // snapshot

    public class CreateSnapshotRequest
    {
        public string Description { get; set; } = string.Empty;
    }

    // dataset

    public class SyntheticDataRequest
    {
        public string Prompt { get; set; } = string.Empty; 
        public string Columns { get; set; } = string.Empty; 
        public int RowCount { get; set; } = 1000;
        public string FileName { get; set; } = "synthetic_data.csv"; // isimlendirme
        public GenerationMode Mode { get; set; } = GenerationMode.FastAndFake;

        // Sadece DigitalTwin iin hangi dosyann kopyalanacag
        public int? SourceFileId { get; set; }
    }
}
