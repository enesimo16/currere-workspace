using System;

namespace Currere_backend.Models
{
    public class ExperimentLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CodeHash { get; set; } = string.Empty;
        public string CodeContent { get; set; } = string.Empty;
        public string? DatasetReference { get; set; }
        public long ExecutionDurationMs { get; set; }
        public string? OutputMetrics { get; set; }
        public string? ArtifactUrls { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
