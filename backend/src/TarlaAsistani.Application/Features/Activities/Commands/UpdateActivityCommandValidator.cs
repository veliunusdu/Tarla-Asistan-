using FluentValidation;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class UpdateActivityCommandValidator : AbstractValidator<UpdateActivityCommand>
{
    public UpdateActivityCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        When(x => x.Description != null, () =>
        {
            RuleFor(x => x.Description)
                .MinimumLength(2)
                .MaximumLength(4000);
        });

        When(x => x.OccurredAt.HasValue, () =>
        {
            RuleFor(x => x.OccurredAt!.Value)
                .Must(date => date <= DateTime.UtcNow.AddMinutes(5))
                .WithMessage("Tamamlanmış faaliyet tarihi gelecekte olamaz.");
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
