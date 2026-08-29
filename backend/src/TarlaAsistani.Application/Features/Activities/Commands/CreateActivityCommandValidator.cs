using FluentValidation;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class CreateActivityCommandValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty();

        RuleFor(x => x.CreatedById)
            .NotEmpty();

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Faaliyet açıklaması boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(4000);

        RuleFor(x => x.OccurredAt)
            .Must(date => date <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Tamamlanmış faaliyet tarihi gelecekte olamaz.");

        RuleFor(x => x.InputMethod)
            .Must(source => source != ActivitySource.Task)
            .WithMessage("TASK kaynaklı faaliyet yalnızca görev tamamlama ile oluşur.");

        When(x => x.InputMethod == ActivitySource.Voice, () =>
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.VoiceUrl) || !string.IsNullOrWhiteSpace(x.VoiceTranscript))
                .WithMessage("Sesli faaliyet için kayıt veya döküm gereklidir.");
        });

        When(x => x.DurationMinutes.HasValue, () =>
        {
            RuleFor(x => x.DurationMinutes!.Value)
                .GreaterThan(0)
                .LessThanOrEqualTo(100_000);
        });

        When(x => x.Amount.HasValue, () =>
        {
            RuleFor(x => x.Amount!.Value)
                .GreaterThan(0)
                .LessThanOrEqualTo(1_000_000_000);
        });

        When(x => x.Cost.HasValue, () =>
        {
            RuleFor(x => x.Cost!.Value)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(1_000_000_000);
        });

        When(x => !string.IsNullOrEmpty(x.Unit), () =>
        {
            RuleFor(x => x.Unit)
                .MaximumLength(40);
        });

        When(x => !string.IsNullOrEmpty(x.PhotoUrl), () =>
        {
            RuleFor(x => x.PhotoUrl)
                .MaximumLength(2048);
        });

        When(x => !string.IsNullOrEmpty(x.VoiceUrl), () =>
        {
            RuleFor(x => x.VoiceUrl)
                .MaximumLength(2048);
        });

        When(x => !string.IsNullOrEmpty(x.PerformedBy), () =>
        {
            RuleFor(x => x.PerformedBy)
                .MaximumLength(120);
        });
    }
}
