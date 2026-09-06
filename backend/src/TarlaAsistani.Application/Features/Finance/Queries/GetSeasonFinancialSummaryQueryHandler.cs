using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Queries;

public sealed class GetSeasonFinancialSummaryQueryHandler : IRequestHandler<GetSeasonFinancialSummaryQuery, FinancialSummaryDto>
{
    private const int RecentSalesLimit = 5;
    private readonly IApplicationDbContext _db;

    public GetSeasonFinancialSummaryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FinancialSummaryDto> Handle(GetSeasonFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var scope = await FinancialScope.LoadAsync(_db, request.FarmId, request.CropPeriodId, request.UserId, cancellationToken);
        if (scope is null) throw new KeyNotFoundException("Tarla veya üretim dönemi bulunamadı.");

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.FarmId == request.FarmId && e.CropPeriodId == request.CropPeriodId && e.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken);
        var sales = await _db.CropSales.AsNoTracking()
            .Where(s => s.FarmId == request.FarmId && s.CropPeriodId == request.CropPeriodId && s.ArchivedAtUtc == null)
            .OrderByDescending(s => s.SoldAt).ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);

        var totalExpense = expenses.Sum(e => e.Amount);
        var totalRevenue = sales.Sum(s => s.TotalAmount);
        decimal? areaInDecares = scope.Farm.SizeInHectares is > 0
            ? Convert.ToDecimal(scope.Farm.SizeInHectares.Value * 10d)
            : null;
        decimal? expensePerDecare = areaInDecares is > 0
            ? Math.Round(totalExpense / areaInDecares.Value, 2, MidpointRounding.AwayFromZero)
            : null;

        var units = sales.Select(s => s.Unit.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        decimal? totalQuantity = units.Count == 1 ? sales.Sum(s => s.HarvestQuantity) : null;
        var quantityUnit = units.Count == 1 ? units[0] : null;

        var breakdown = expenses
            .GroupBy(e => e.Category)
            .Select(group => new ExpenseBreakdownDto(
                group.Key,
                group.Sum(e => e.Amount),
                totalExpense == 0 ? 0 : Math.Round(group.Sum(e => e.Amount) / totalExpense * 100m, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(item => item.Amount)
            .ToList();

        var warnings = new List<string>
        {
            "Kayıtlı gelir-gider farkı yalnızca sisteme girilen verileri yansıtır."
        };
        if (expenses.Count == 0) warnings.Add("Henüz kayıtlı gider bulunmuyor.");
        if (sales.Count == 0) warnings.Add("Henüz kayıtlı gelir bulunmuyor.");
        if (areaInDecares is null) warnings.Add("Tarla alan bilgisi girilmediği için dekar başına gider hesaplanamadı.");

        return new FinancialSummaryDto(
            scope.Farm.Id,
            scope.Farm.Name,
            scope.CropPeriod.Id,
            scope.CropPeriod.CropName,
            scope.CropPeriod.Variety,
            scope.CropPeriod.HarvestedAt?.Year ?? scope.CropPeriod.PlantedAt.Year,
            scope.CropPeriod.PlantedAt,
            scope.CropPeriod.HarvestedAt,
            areaInDecares,
            totalExpense,
            totalRevenue,
            totalRevenue - totalExpense,
            expensePerDecare,
            totalQuantity,
            quantityUnit,
            expenses.Count > 0,
            sales.Count > 0,
            breakdown,
            sales.Take(RecentSalesLimit).Select(CropSaleDtoMapper.Map).ToList(),
            warnings,
            areaInDecares is not null);
    }
}
