using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, ExpenseDto?>
{
    private readonly IApplicationDbContext _db;

    public UpdateExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ExpenseDto?> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(
            e => e.Id == request.ExpenseId && e.CreatedById == request.UserId && e.ArchivedAtUtc == null,
            cancellationToken);
        if (expense is null) return null;
        var farm = await _db.Farms.FirstOrDefaultAsync(
            f => f.Id == expense.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null,
            cancellationToken);
        if (farm is null) return null;
        if (expense.ActivityId.HasValue)
            throw new InvalidOperationException("Activity-linked expenses must be updated through the activity.");

        expense.Category = request.Category;
        expense.Amount = request.Amount;
        expense.OccurredAtUtc = request.OccurredAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.OccurredAtUtc, DateTimeKind.Utc)
            : request.OccurredAtUtc.ToUniversalTime();
        expense.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        expense.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseDtoMapper.Map(expense);
    }
}
