namespace Currere_backend.Services
{
    public interface IAiService
    {
        Task<string> ChatAsync(string message, string? systemContext = null);

        Task<string> GeneratePythonCodeAsync(string userPrompt, string datasetProfileJson, string fileName);

        Task<string> GenerateInlineCompletionAsync(string code, int cursorLine, int cursorCol);

        Task<string> DetermineIntentAsync(string userMessage); // chat or code
    }
}