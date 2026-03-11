using Currere_backend.DTOs;
using FluentValidation;

namespace Currere_backend.Helpers
{
    // AbstractValidator<dto> klasik yonergemiz
    public class ExecuteCodeDtoValidator : AbstractValidator<ExecuteCodeDto>
    {
        public ExecuteCodeDtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Çalıştırılacak kod alanı boş bırakılamaz.")
                .MinimumLength(5).WithMessage("Lütfen mantıklı bir Python kodu girin (en az 5 karakter).")
                .MaximumLength(10000).WithMessage("Kodunuz çok uzun. Sandbox sınırlarını aşıyor.");
        }
    }
}