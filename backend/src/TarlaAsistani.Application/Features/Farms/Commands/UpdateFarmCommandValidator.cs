using FluentValidation;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class UpdateFarmCommandValidator : AbstractValidator<UpdateFarmCommand>
{
    public UpdateFarmCommandValidator()
    {
        RuleFor(v => v.FarmId)
            .NotEmpty().WithMessage("FarmId is required.");

        When(v => v.Name != null, () =>
        {
            RuleFor(v => v.Name)
                .NotEmpty().WithMessage("Farm name cannot be empty.")
                .MinimumLength(2).WithMessage("Farm name must be at least 2 characters.")
                .MaximumLength(120).WithMessage("Farm name must not exceed 120 characters.");
        });

        When(v => v.Latitude.HasValue, () =>
        {
            RuleFor(v => v.Latitude!.Value)
                .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90 and 90.");
        });

        When(v => v.Longitude.HasValue, () =>
        {
            RuleFor(v => v.Longitude!.Value)
                .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180 and 180.");
        });

        When(v => v.SizeInHectares.HasValue, () =>
        {
            RuleFor(v => v.SizeInHectares!.Value)
                .GreaterThan(0).WithMessage("Size must be greater than 0.")
                .LessThanOrEqualTo(1_000_000).WithMessage("Size cannot exceed 1,000,000 hectares.");
        });

        When(v => v.SoilType != null, () =>
        {
            RuleFor(v => v.SoilType)
                .MaximumLength(80).WithMessage("Soil type must not exceed 80 characters.");
        });

        When(v => v.Note != null, () =>
        {
            RuleFor(v => v.Note)
                .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
        });
    }
}
