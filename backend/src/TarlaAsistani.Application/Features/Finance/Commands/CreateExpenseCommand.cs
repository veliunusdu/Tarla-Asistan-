using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record CreateExpenseCommand(
    Guid FarmId,
    Guid CropPeriodId,
    Guid UserId,
    ExpenseCategory Category,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Note = null,
    Guid? ClientOperationId = null) : IRequest<ExpenseDto>;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.CropPeriodId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ClientOperationId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Category).IsInEnum();
    }
}
