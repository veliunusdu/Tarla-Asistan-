using FluentValidation;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class CreateExpertTaskCommandValidator : AbstractValidator<CreateExpertTaskCommand>
{
    public CreateExpertTaskCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty();

        RuleFor(x => x.CreatedById)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Görev başlığı boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(160);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Görev açıklaması boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(4000);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Görev nedeni boş olamaz.")
            .MinimumLength(2)
            .MaximumLength(2000);
    }
}
