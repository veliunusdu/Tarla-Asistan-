using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Queries;

public sealed class ListExpensesQueryHandler : IRequestHandler<ListExpensesQuery, IReadOnlyList<ExpenseDto>>
{
    private readonly IApplicationDbContext _db;

    public ListExpensesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExpenseDto>> Handle(ListExpensesQuery request, CancellationToken cancellationToken)
    {
        var scope = await FinancialScope.LoadAsync(_db, request.FarmId, request.CropPeriodId, request.UserId, cancellationToken);
        if (scope is null) throw new KeyNotFoundException("Tarla veya üretim dönemi bulunamadı.");

        return await _db.Expenses.AsNoTracking()
            .Where(e => e.FarmId == request.FarmId && e.CropPeriodId == request.CropPeriodId && e.ArchivedAtUtc == null)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Select(e => new ExpenseDto(e.Id, e.FarmId, e.CropPeriodId, e.Category, e.Amount, e.OccurredAtUtc,
                e.Note, e.ActivityId.HasValue, e.ActivityId, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
