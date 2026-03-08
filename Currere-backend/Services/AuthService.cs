using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Currere_backend.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string> RegisterAsync(RegisterDto request)
        {
            string cleanEmail = request.Email.Trim().ToLower();

            if (await _context.Users.AnyAsync(u => u.Email == cleanEmail))
            {
                throw new Exception("Bu email adresi zaten kullanýmda.");
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                FirstName = request.FirstName.Trim(), 
                LastName = request.LastName.Trim(),   
                Email = cleanEmail,
                PasswordHash = passwordHash,
                Role = UserRole.User
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return "Kayýt baþarýlý!";
        }

        public async Task<string> LoginAsync(LoginDto request)
        {
            string cleanEmail = request.Email.Trim().ToLower();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail);

            if (user == null)
            {
                throw new Exception("Kullanýcý bulunamadý.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new Exception("Hatalý þifre.");
            }

            return CreateToken(user);
        }

        private string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("JwtSettings:Secret").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                issuer: _configuration.GetSection("JwtSettings:Issuer").Value,
                audience: _configuration.GetSection("JwtSettings:Audience").Value,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration.GetSection("JwtSettings:ExpiryMinutes").Value)),
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }
    }
}