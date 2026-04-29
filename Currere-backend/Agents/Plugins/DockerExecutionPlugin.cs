using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Currere_backend.Models;
using Currere_backend.Services;
using Microsoft.SemanticKernel;

namespace Currere_backend.Agents.Plugins
{
    /// <summary>
    /// Ajanların Docker sandbox ortamında saf Python kodlarını çalıştırabilmesini sağlayan Semantic Kernel eklentisi (uzvu).
    /// </summary>
    public class DockerExecutionPlugin
    {
        private readonly ICodeExecutionService _codeExecutionService;

        public DockerExecutionPlugin(ICodeExecutionService codeExecutionService)
        {
            _codeExecutionService = codeExecutionService ?? throw new ArgumentNullException(nameof(codeExecutionService));
        }

        /// <summary>
        /// Ajan tarafından üretilen Python kodunu izole Docker konteynerine gönderir ve Self-Healing ajanı için formatlanmış yanıt döner.
        /// </summary>
        [KernelFunction("RunPythonInSandbox")]
        [Description("Verilen saf Python kodunu izole bir Docker sandbox ortamında çalıştırır. Self-Healing mekanizması için tasarlanmıştır; başarılıysa SUCCESS ve çıktıyı, başarısızsa ERROR ve detayları döner.")]
        public async Task<string> RunPythonInSandboxAsync(
            [Description("Çalıştırılacak saf, çalıştırılabilir Python kodu.")] string pythonCode,
            [Description("Kodun izole olarak çalıştırılacağı Workspace'in (Çalışma Alanı) ID numarası.")] int workspaceId)
        {
            // Execution işlemi için Job modeli oluşturulur.
            var job = new ExecutionJob
            {
                JobId = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                Code = pythonCode
            };

            // Faz 2'de yazılan güvenli altyapı çağrılır.
            var result = await _codeExecutionService.ExecutePythonCodeAsync(job);

            // Self-Healing (Şifacı) ajanının hatayı veya başarıyı net olarak ayrıştırabilmesi için formatlanmış çıktı döner.
            if (result.IsSuccess)
            {
                return $"SUCCESS:\n{result.Output}";
            }
            else
            {
                return $"ERROR:\nError Type: {result.ErrorType}\nDetails: {result.Error}";
            }
        }
    }
}
