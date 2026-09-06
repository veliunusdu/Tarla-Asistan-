using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Finance.DTOs;

public record ExpenseDto(
    Guid Id,
    Guid FarmId,
    Guid CropPeriodId,
    ExpenseCategory Category,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Note,
    bool IsActivityLinked,
    Guid? ActivityId,
    DateTime CreatedAtUtc);

public record CropSaleDto(
    Guid Id,
    Guid FarmId,
    Guid CropPeriodId,
    decimal HarvestQuantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalAmount,
    DateOnly SoldAt,
    string? BuyerOrMarketNote,
    DateTime CreatedAtUtc);

public record ExpenseBreakdownDto(ExpenseCategory Category, decimal Amount, decimal Percentage);

public record FinancialSummaryDto(
    Guid FarmId,
    string FarmName,
    Guid CropPeriodId,
    string CropName,
    string? Variety,
    int SeasonYear,
    DateOnly PlantedAt,
    DateOnly? HarvestedAt,
    decimal? AreaInDecares,
    decimal TotalExpense,
    decimal TotalRevenue,
    decimal RegisteredDifference,
    decimal? ExpensePerDecare,
    decimal? TotalQuantitySold,
    string? QuantityUnit,
    bool HasExpenseRecords,
    bool HasRevenueRecords,
    IReadOnlyList<ExpenseBreakdownDto> ExpenseBreakdown,
    IReadOnlyList<CropSaleDto> RecentSales,
    IReadOnlyList<string> Warnings,
    bool IsAreaDefined);
