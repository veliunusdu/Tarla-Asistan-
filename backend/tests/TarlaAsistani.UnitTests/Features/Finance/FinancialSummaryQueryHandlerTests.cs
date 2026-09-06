using FluentAssertions;
using TarlaAsistani.Application.Features.Finance.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Finance;

public sealed class FinancialSummaryQueryHandlerTests
{
    [Fact]
    public async Task Summary_should_sum_active_expenses_and_sales_without_counting_activity_cost_again()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla", SizeInHectares = 2.5 };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var activity = new Activity { FarmId = farm.Id, CropPeriodId = period.Id, Cost = 999f };
        var linked = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, ActivityId = activity.Id, Amount = 100m };
        var standalone = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, Amount = 50m };
        var sale = new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, HarvestQuantity = 10m, Unit = "kg", UnitPrice = 20m, TotalAmount = 200m, SoldAt = new DateOnly(2026, 7, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithActivities(activity).WithExpenses(linked, standalone).WithCropSales(sale).Build();

        var result = await new GetSeasonFinancialSummaryQueryHandler(db).Handle(
            new GetSeasonFinancialSummaryQuery(farm.Id, period.Id, ownerId), CancellationToken.None);

        result.TotalExpense.Should().Be(150m);
        result.TotalRevenue.Should().Be(200m);
        result.RegisteredDifference.Should().Be(50m);
        result.ExpensePerDecare.Should().Be(6m);
    }

    [Fact]
    public async Task Summary_should_not_naively_sum_different_sale_units()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var sales = new[]
        {
            new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, HarvestQuantity = 1000m, Unit = "kg", UnitPrice = 1m, TotalAmount = 1000m, SoldAt = new DateOnly(2026, 7, 1) },
            new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, HarvestQuantity = 2m, Unit = "ton", UnitPrice = 900m, TotalAmount = 1800m, SoldAt = new DateOnly(2026, 7, 2) }
        };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithCropSales(sales).Build();

        var result = await new GetSeasonFinancialSummaryQueryHandler(db).Handle(
            new GetSeasonFinancialSummaryQuery(farm.Id, period.Id, ownerId), CancellationToken.None);

        result.TotalQuantitySold.Should().BeNull();
        result.QuantityUnit.Should().BeNull();
        result.TotalRevenue.Should().Be(2800m);
    }

    [Fact]
    public async Task Summary_should_sum_same_sale_units_and_exclude_archived_records_with_warnings()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla", SizeInHectares = 1 };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var activeSale = new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, HarvestQuantity = 3m, Unit = "kg", UnitPrice = 10m, TotalAmount = 30m, SoldAt = new DateOnly(2026, 7, 1) };
        var archivedSale = new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, HarvestQuantity = 100m, Unit = "kg", UnitPrice = 10m, TotalAmount = 1000m, SoldAt = new DateOnly(2026, 7, 2), ArchivedAtUtc = DateTime.UtcNow };
        var archivedExpense = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, Amount = 500m, ArchivedAtUtc = DateTime.UtcNow };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithCropSales(activeSale, archivedSale).WithExpenses(archivedExpense).Build();

        var result = await new GetSeasonFinancialSummaryQueryHandler(db).Handle(
            new GetSeasonFinancialSummaryQuery(farm.Id, period.Id, ownerId), CancellationToken.None);

        result.TotalQuantitySold.Should().Be(3m);
        result.QuantityUnit.Should().Be("kg");
        result.TotalRevenue.Should().Be(30m);
        result.TotalExpense.Should().Be(0m);
        result.Warnings.Should().Contain("Henüz kayıtlı gider bulunmuyor.");
    }

    [Fact]
    public async Task Summary_should_warn_when_area_is_missing_and_sales_are_missing()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).Build();

        var result = await new GetSeasonFinancialSummaryQueryHandler(db).Handle(
            new GetSeasonFinancialSummaryQuery(farm.Id, period.Id, ownerId), CancellationToken.None);

        result.ExpensePerDecare.Should().BeNull();
        result.Warnings.Should().Contain("Henüz kayıtlı gelir bulunmuyor.");
        result.Warnings.Should().Contain("Tarla alan bilgisi girilmediği için dekar başına gider hesaplanamadı.");
    }
}
