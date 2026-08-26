using FluentValidation;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public class CloseCropPeriodCommandValidator : AbstractValidator<CloseCropPeriodCommand>
{
    public CloseCropPeriodCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty();

        RuleFor(x => x.PeriodId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.HarvestedAt)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Hasat tarihi gelecekte olamaz.");
    }
}
