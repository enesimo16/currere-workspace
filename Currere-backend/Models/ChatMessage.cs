using Currere_backend.DTOs;

namespace Currere_backend.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int WorkspaceId { get; set; }
        public MessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public Workspace Workspace { get; set; } = null!;
    }
}