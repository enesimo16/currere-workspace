using System.Collections.Generic;
using System.Threading.Tasks;

namespace Currere_backend.Services
{
    public class CodePreProcessResultDto
    {
        public string Code { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new List<string>();
    }

    public interface ICodePreProcessorService
    {
        /// <summary>
        /// Gelen Python veya Jupyter Notebook kodunu işler ve güvenlik taramasından geçirir.
        /// Başarılıysa saf Python kodunu ve bağımlılıklarını döndürür.
        /// Güvenlik veya sözdizimi hatası varsa exception fırlatır.
        /// </summary>
        Task<CodePreProcessResultDto> ProcessCodeAsync(string rawCode);
    }
}
