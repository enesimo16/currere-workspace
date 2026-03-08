using System.ComponentModel.DataAnnotations;

namespace Currere_backend.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Ad alaný zorunludur.")]
        [MinLength(2, ErrorMessage = "Ad en az 2 karakter olmalýdýr.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alaný zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email alaný zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Þifre alaný zorunludur.")]
        [MinLength(6, ErrorMessage = "Þifre en az 6 karakter olmalýdýr.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Email alaný zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Þifre zorunludur.")]
        public string Password { get; set; } = string.Empty;
    }
}