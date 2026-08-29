using FluentValidation;

namespace TarlaAsistani.Application.Features.Notifications.Commands;

public class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Cihaz tokenı zorunludur.")
            .MaximumLength(512);
    }
}
