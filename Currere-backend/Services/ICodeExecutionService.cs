using Currere_backend.DTOs;
using Currere_backend.Models;

namespace Currere_backend.Services
{
    public interface ICodeExecutionService
    {
        Task<ExecutionResultDto> ExecutePythonCodeAsync(ExecutionJob job);
    }
}