namespace Currere_backend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;  
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; 
        public UserRole Role { get; set; } = UserRole.User;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // student pack 2.mail

        public string? StudentEmail { get; set; } // Örn: name@sakarya.edu.tr
        public bool IsStudentEmailVerified { get; set; } = false; 




        // Mevcut User sýnýfýnýn içine, en alt satýra ekle:
        public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
    }
}