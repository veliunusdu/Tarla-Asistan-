using FluentValidation;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class CreateFarmCommandValidator : AbstractValidator<CreateFarmCommand>
{
    public CreateFarmCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Farm name is required.")
            .MaximumLength(120).WithMessage("Name must not exceed 120 characters.");

        RuleFor(v => v.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(v => v.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180 and 180.");

        RuleFor(v => v.InitialPlantedAt)
            .NotEmpty().WithMessage("Planting date is required.");
    }
}