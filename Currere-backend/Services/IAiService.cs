namespace Currere_backend.Services
{
    public interface IAiService
    {
        Task<string> ChatAsync(string message, string? systemContext = null);

        Task<string> GeneratePythonCodeAsync(string userPrompt, string datasetProfileJson, string fileName);

        Task<string> DetermineIntentAsync(string userMessage); // chat or code
    }
}