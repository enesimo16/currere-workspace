using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Sistem Hatası: " + ex.Message });
            }
        }

        [Authorize] // Sadece giris yapanlar student pack yapabilir
        [HttpPost("link-student")]
        public async Task<IActionResult> LinkStudentAccount([FromBody] LinkStudentEmailDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var success = await _authService.LinkStudentEmailAsync(userId, dto.StudentEmail);

                if (success)
                {
                    return Ok(new { message = "Öğrenci hesabınız başarıyla bağlandı! Ayrıcalıklardan yararlanmak için lütfen tekrar giriş yapıp yeni Token alın." });
                }

                return BadRequest(new { message = "Kullanıcı bulunamadı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}