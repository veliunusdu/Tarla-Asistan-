using FluentValidation;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class CreateCaseCommandValidator : AbstractValidator<CreateCaseCommand>
{
    public CreateCaseCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty();

        RuleFor(x => x.CreatedById)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Vaka başlığı boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(160);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Vaka açıklaması boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(4000);
    }
}
