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

        RuleFor(x => x.CropName)
            .Cascade(CascadeMode.Stop)
            .Must(name => name == string.Empty || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Ürün adı sadece boşluk olamaz.")
            .Must((cmd, name) => !string.IsNullOrWhiteSpace(name) || cmd.CropType.HasValue)
            .WithMessage("Ürün adı gereklidir.")
            .MaximumLength(100).WithMessage("Ürün adı en fazla 100 karakter olabilir.");

        When(x => !string.IsNullOrEmpty(x.Variety), () =>
        {
            RuleFor(x => x.Variety)
                .MaximumLength(120);
        });
    }
}
