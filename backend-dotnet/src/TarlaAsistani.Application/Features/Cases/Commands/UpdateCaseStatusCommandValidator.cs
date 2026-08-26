using FluentValidation;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class UpdateCaseStatusCommandValidator : AbstractValidator<UpdateCaseStatusCommand>
{
    public UpdateCaseStatusCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
