using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Currere_backend.Agents.Models
{
    /// <summary>
    /// Architect ajanı tarafından üretilecek katı tipteki JSON plan modeli.
    /// Semantic Kernel'ın çıktısı doğrudan bu objeye (Type-Safe) Deserialize edilecektir.
    /// </summary>
    public class ExecutionPlan
    {
        [JsonPropertyName("goal")]
        public string Goal { get; set; } = string.Empty;

        [JsonPropertyName("steps")]
        public List<string> Steps { get; set; } = new List<string>();

        [JsonPropertyName("required_libraries")]
        public List<string> RequiredLibraries { get; set; } = new List<string>();
    }
}
