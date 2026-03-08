namespace Currere_backend.Models
{
    public class WorkspaceSnapshot
    {
        public int Id { get; set; }
        public int WorkspaceId { get; set; }
        public string CodeContent { get; set; } = string.Empty; // O anki kodun kopyası
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Workspace Workspace { get; set; } = null!;
    }
}