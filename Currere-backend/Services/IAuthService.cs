using Currere_backend.DTOs;

namespace Currere_backend.Services
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(RegisterDto request);
        Task<string> LoginAsync(LoginDto request);
    }
}