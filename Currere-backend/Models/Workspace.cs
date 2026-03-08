using Currere_backend.DTOs;

namespace Currere_backend.Models
{
    public class Workspace
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = "Untitled Workspace";
        public WorkspaceFormat Format { get; set; } = WorkspaceFormat.Python;
        public RuntimeType Runtime { get; set; } = RuntimeType.CPU;

        // auto save or manuel save anında kaydetme
        public string CurrentState { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // properties
        public User User { get; set; } = null!;
        public ICollection<WorkspaceSecret> Secrets { get; set; } = new List<WorkspaceSecret>();
        public ICollection<WorkspaceFile> Files { get; set; } = new List<WorkspaceFile>();
        public ICollection<WorkspaceSnapshot> Snapshots { get; set; } = new List<WorkspaceSnapshot>();
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}