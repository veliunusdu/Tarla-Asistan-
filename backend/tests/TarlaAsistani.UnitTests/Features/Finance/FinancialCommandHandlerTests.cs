using FluentAssertions;
using TarlaAsistani.Application.Features.Finance.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Finance;

public sealed class FinancialCommandHandlerTests
{
    [Fact]
    public async Task CreateExpense_should_create_valid_expense_for_farm_owner()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).Build();

        var result = await new CreateExpenseCommandHandler(db).Handle(new CreateExpenseCommand(
            farm.Id, period.Id, ownerId, ExpenseCategory.Fertilizer, 1250.50m,
            DateTime.UtcNow, "Üre"), CancellationToken.None);

        result.Amount.Should().Be(1250.50m);
        result.Category.Should().Be(ExpenseCategory.Fertilizer);
    }

    [Fact]
    public async Task CreateExpense_should_reject_another_tenant_period()
    {
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var otherFarm = new Farm { OwnerId = otherOwnerId, Name = "Diğer" };
        var period = new CropPeriod { FarmId = otherFarm.Id, CropName = "Mısır", PlantedAt = new DateOnly(2026, 1, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm, otherFarm).WithCropPeriods(period).Build();

        var act = () => new CreateExpenseCommandHandler(db).Handle(new CreateExpenseCommand(
            farm.Id, period.Id, ownerId, ExpenseCategory.Fuel, 100m,
            DateTime.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateExpense_validator_should_reject_zero_and_negative_amounts()
    {
        var validator = new CreateExpenseCommandValidator();
        var zero = await validator.ValidateAsync(new CreateExpenseCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ExpenseCategory.Other, 0m, DateTime.UtcNow));
        var negative = await validator.ValidateAsync(new CreateExpenseCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ExpenseCategory.Other, -1m, DateTime.UtcNow));

        zero.IsValid.Should().BeFalse();
        negative.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateExpense_should_reject_activity_linked_expense()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var expense = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, ActivityId = Guid.NewGuid(), Amount = 100m };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithExpenses(expense).Build();

        var act = () => new UpdateExpenseCommandHandler(db).Handle(new UpdateExpenseCommand(
            expense.Id, ownerId, ExpenseCategory.Fuel, 200m, DateTime.UtcNow, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ArchiveExpense_should_soft_archive_standalone_expense()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var expense = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, Amount = 100m };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithExpenses(expense).Build();

        (await new ArchiveExpenseCommandHandler(db).Handle(new ArchiveExpenseCommand(expense.Id, ownerId), CancellationToken.None))
            .Should().BeTrue();
        expense.ArchivedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCropSale_should_calculate_total_amount_on_server()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).Build();

        var result = await new CreateCropSaleCommandHandler(db).Handle(new CreateCropSaleCommand(
            farm.Id, period.Id, ownerId, 10.005m, "kg", 2.345m, new DateOnly(2026, 7, 1)), CancellationToken.None);

        result.TotalAmount.Should().Be(23.46m);
    }

    [Fact]
    public async Task CreateCropSale_should_reject_zero_quantity()
    {
        var validator = new CreateCropSaleCommandValidator();
        var result = await validator.ValidateAsync(new CreateCropSaleCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, "kg", 2m, new DateOnly(2026, 7, 1)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCropSale_should_recalculate_total_amount()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var sale = new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, HarvestQuantity = 1m, Unit = "kg", UnitPrice = 2m, TotalAmount = 2m, SoldAt = new DateOnly(2026, 7, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithCropSales(sale).Build();

        var result = await new UpdateCropSaleCommandHandler(db).Handle(new UpdateCropSaleCommand(
            sale.Id, ownerId, 3m, "kg", 4m, new DateOnly(2026, 7, 2)), CancellationToken.None);

        result!.TotalAmount.Should().Be(12m);
    }

    [Fact]
    public async Task ArchiveCropSale_should_soft_archive_sale()
    {
        var ownerId = Guid.NewGuid();
        var farm = new Farm { OwnerId = ownerId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var sale = new CropSale { FarmId = farm.Id, CropPeriodId = period.Id, CreatedById = ownerId, HarvestQuantity = 1m, Unit = "kg", UnitPrice = 2m, TotalAmount = 2m, SoldAt = new DateOnly(2026, 7, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithCropSales(sale).Build();

        (await new ArchiveCropSaleCommandHandler(db).Handle(new ArchiveCropSaleCommand(sale.Id, ownerId), CancellationToken.None))
            .Should().BeTrue();
        sale.ArchivedAtUtc.Should().NotBeNull();
    }
}
