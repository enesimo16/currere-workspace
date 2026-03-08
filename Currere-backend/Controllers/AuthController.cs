using Microsoft.AspNetCore.Mvc;
using Currere_backend.DTOs;
using Currere_backend.Services;

namespace Currere_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // DI
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto request)
        {
            try
            {
                var token = await _authService.LoginAsync(request);
                return Ok(new { token = token }); 
            }
            catch (Exception ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
        }
    }
}