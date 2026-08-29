using FluentValidation;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public class CreateCropPeriodCommandValidator : AbstractValidator<CreateCropPeriodCommand>
{
    public CreateCropPeriodCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty();

        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.PlantedAt)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ekim tarihi gelecekte olamaz.");

        When(x => !string.IsNullOrEmpty(x.Variety), () =>
        {
            RuleFor(x => x.Variety)
                .MaximumLength(120);
        });
    }
}
