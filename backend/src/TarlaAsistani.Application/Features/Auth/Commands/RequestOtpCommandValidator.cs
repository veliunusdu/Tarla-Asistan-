using System.Text.RegularExpressions;
using FluentValidation;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon numarası zorunludur.")
            .Must(phone => !string.IsNullOrWhiteSpace(phone) && Regex.IsMatch(phone.Trim(), @"^\+?[1-9]\d{1,14}$"))
            .WithMessage("Telefon numarası uluslararası E.164 biçiminde olmalıdır.");
    }
}
