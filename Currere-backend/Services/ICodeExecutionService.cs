using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface ICodeExecutionService
    {
        Task<ExecutionResultDto> ExecutePythonCodeAsync(int workspaceId, string code);
    }
}