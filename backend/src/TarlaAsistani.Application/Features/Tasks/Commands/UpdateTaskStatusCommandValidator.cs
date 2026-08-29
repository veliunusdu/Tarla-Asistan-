using FluentValidation;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    private static readonly TaskStatus[] AllowedStatuses = [
        TaskStatus.Viewed,
        TaskStatus.Planned,
        TaskStatus.Completed,
        TaskStatus.NotApplied,
        TaskStatus.Cancelled
    ];

    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Status)
            .Must(status => AllowedStatuses.Contains(status))
            .WithMessage("Bu görev durumuna doğrudan geçilemez.");

        When(x => x.Status == TaskStatus.NotApplied, () =>
        {
            RuleFor(x => x.NotAppliedReason)
                .NotEmpty().WithMessage("Uygulanmama nedeni zorunludur.")
                .MaximumLength(500);
        });

        When(x => !string.IsNullOrEmpty(x.Note), () =>
        {
            RuleFor(x => x.Note)
                .MaximumLength(1000);
        });

        When(x => !string.IsNullOrEmpty(x.PhotoUrl), () =>
        {
            RuleFor(x => x.PhotoUrl)
                .MaximumLength(2048);
        });
    }
}
