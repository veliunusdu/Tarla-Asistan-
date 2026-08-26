using System.Text.RegularExpressions;
using FluentValidation;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon numarası zorunludur.")
            .Must(phone => !string.IsNullOrWhiteSpace(phone) && Regex.IsMatch(phone.Trim(), @"^\+?[1-9]\d{1,14}$"))
            .WithMessage("Telefon numarası uluslararası E.164 biçiminde olmalıdır.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Doğrulama kodu zorunludur.")
            .Matches(@"^\d{6}$").WithMessage("Doğrulama kodu 6 haneli olmalıdır.");
    }
}
