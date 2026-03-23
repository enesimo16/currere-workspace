using System;
using Currere_backend.DTOs;

namespace Currere_backend.Models
{
    public class ExecutionJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public int WorkspaceId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? DatasetFileName { get; set; }
        
        /// <summary>
        /// "Processing", "Completed", "Failed"
        /// </summary>
        public string Status { get; set; } = "Processing";
        
        public ExecutionResultDto? Result { get; set; }
    }
}
