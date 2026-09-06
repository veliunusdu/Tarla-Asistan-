using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record UpdateExpenseCommand(
    Guid ExpenseId,
    Guid UserId,
    ExpenseCategory Category,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Note = null) : IRequest<ExpenseDto?>;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Category).IsInEnum();
    }
}
