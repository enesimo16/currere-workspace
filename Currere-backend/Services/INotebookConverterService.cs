namespace Currere_backend.Services
{
    public interface INotebookConverterService
    {
        // IPYNB JSON içeriğini alıp sadece kodları birleştirip pure py
        Task<string> ExtractRawPythonFromNotebookAsync(string ipynbContent);
        // pure py yi ai ya yollama
        Task<string> CleanAndOptimizePythonCodeAsync(string rawPythonCode);
    }
}