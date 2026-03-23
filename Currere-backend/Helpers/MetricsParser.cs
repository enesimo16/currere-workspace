using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Currere_backend.Helpers
{
    public static class MetricsParser
    {
        public static string? ParseMetrics(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return null;

            var metrics = new Dictionary<string, string>();
            // Regex to catch standard ML metrics e.g., Accuracy: 0.95 or Loss = 1.2
            var regex = new Regex(@"(?i)(Accuracy|Loss|F1-Score|Precision|Recall|MSE|MAE)\s*[:=]\s*([0-9\.]+)", RegexOptions.IgnoreCase);
            
            var matches = regex.Matches(output);
            if (matches.Count == 0) return null;

            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                // Ensure the last key overrides if outputting multiple epochs
                metrics[key] = value;
            }

            return JsonSerializer.Serialize(metrics);
        }
    }
}
