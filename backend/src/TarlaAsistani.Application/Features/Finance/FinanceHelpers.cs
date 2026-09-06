using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Finance;

internal static class FinancialMath
{
    public static decimal CalculateSaleTotal(decimal quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

    public static decimal NormalizeActivityCost(float cost) =>
        Math.Round(Convert.ToDecimal(cost), 2, MidpointRounding.AwayFromZero);

    public static ExpenseCategory MapActivityCategory(ActivityType? type) => type switch
    {
        ActivityType.Fertilization => ExpenseCategory.Fertilizer,
        ActivityType.Spraying => ExpenseCategory.Pesticide,
        ActivityType.Irrigation => ExpenseCategory.Irrigation,
        _ => ExpenseCategory.Other
    };
}

internal static class ExpenseDtoMapper
{
    public static ExpenseDto Map(Expense expense) => new(
        expense.Id, expense.FarmId, expense.CropPeriodId, expense.Category, expense.Amount,
        expense.OccurredAtUtc, expense.Note, expense.ActivityId.HasValue, expense.ActivityId,
        expense.CreatedAtUtc);
}

internal static class CropSaleDtoMapper
{
    public static CropSaleDto Map(CropSale sale) => new(
        sale.Id, sale.FarmId, sale.CropPeriodId, sale.HarvestQuantity, sale.Unit,
        sale.UnitPrice, sale.TotalAmount, sale.SoldAt, sale.BuyerOrMarketNote,
        sale.CreatedAtUtc);
}

internal sealed record FinancialScope(Farm Farm, CropPeriod CropPeriod)
{
    public static async Task<FinancialScope?> LoadAsync(
        IApplicationDbContext db,
        Guid farmId,
        Guid cropPeriodId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var farm = await db.Farms.FirstOrDefaultAsync(
            f => f.Id == farmId && f.OwnerId == userId && f.ArchivedAt == null,
            cancellationToken);
        if (farm is null) return null;

        var cropPeriod = await db.CropPeriods.FirstOrDefaultAsync(
            cp => cp.Id == cropPeriodId && cp.FarmId == farmId,
            cancellationToken);
        return cropPeriod is null ? null : new FinancialScope(farm, cropPeriod);
    }
}
