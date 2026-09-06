using MediatR;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record ArchiveExpenseCommand(Guid ExpenseId, Guid UserId) : IRequest<bool>;
