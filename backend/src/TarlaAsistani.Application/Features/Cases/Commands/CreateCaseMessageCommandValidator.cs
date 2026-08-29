using FluentValidation;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class CreateCaseMessageCommandValidator : AbstractValidator<CreateCaseMessageCommand>
{
    public CreateCaseMessageCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.SenderId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Mesaj içeriği boş olamaz.");
    }
}
