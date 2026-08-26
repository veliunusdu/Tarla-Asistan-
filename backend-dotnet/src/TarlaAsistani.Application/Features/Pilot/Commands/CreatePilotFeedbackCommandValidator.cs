using FluentValidation;

namespace TarlaAsistani.Application.Features.Pilot.Commands;

public class CreatePilotFeedbackCommandValidator : AbstractValidator<CreatePilotFeedbackCommand>
{
    public CreatePilotFeedbackCommandValidator()
    {
        RuleFor(x => x.CreatedById).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty().WithMessage("Geri bildirim açıklaması boş olamaz.");

        When(x => x.Rating.HasValue, () =>
        {
            RuleFor(x => x.Rating!.Value)
                .InclusiveBetween(1, 5)
                .WithMessage("Puan 1 ile 5 arasında olmalıdır.");
        });
    }
}
