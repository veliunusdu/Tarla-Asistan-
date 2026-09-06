using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class ArchiveExpenseCommandHandler : IRequestHandler<ArchiveExpenseCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public ArchiveExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<bool> Handle(ArchiveExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(
            e => e.Id == request.ExpenseId && e.CreatedById == request.UserId && e.ArchivedAtUtc == null,
            cancellationToken);
        if (expense is null) return false;
        var farm = await _db.Farms.FirstOrDefaultAsync(
            f => f.Id == expense.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null,
            cancellationToken);
        if (farm is null) return false;
        if (expense.ActivityId.HasValue)
            throw new InvalidOperationException("Activity-linked expenses must be archived through the activity.");

        expense.ArchivedAtUtc = DateTime.UtcNow;
        expense.UpdatedAtUtc = expense.ArchivedAtUtc.Value;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
