using FluentAssertions;
using TarlaAsistani.Application.Features.Activities.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Activities;

public sealed class ActivityExpenseSyncTests
{
    [Fact]
    public async Task Creating_costed_activity_should_create_one_normalized_expense()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).Build();

        var result = await new CreateActivityCommandHandler(db).Handle(new CreateActivityCommand(
            farm.Id, userId, ActivityName: "Sulama", ActivityType: ActivityType.Irrigation,
            CropPeriodId: period.Id, Cost: 1500.125f, OccurredAt: DateTime.UtcNow), CancellationToken.None);

        var expenses = db.Expenses.ToList();
        expenses.Should().ContainSingle();
        expenses[0].ActivityId.Should().Be(result.Id);
        expenses[0].Amount.Should().Be(1500.13m);
        expenses[0].Category.Should().Be(ExpenseCategory.Irrigation);
    }

    [Fact]
    public async Task Creating_activity_without_cost_should_not_create_expense()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var db = new MockDbContextBuilder().WithFarms(farm).Build();

        await new CreateActivityCommandHandler(db).Handle(new CreateActivityCommand(
            farm.Id, userId, ActivityName: "Kontrol", OccurredAt: DateTime.UtcNow), CancellationToken.None);

        db.Expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task Updating_activity_cost_to_zero_should_archive_linked_expense()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var activity = new Activity { FarmId = farm.Id, Farm = farm, CreatedById = userId, Cost = 100f, ActivityType = ActivityType.Irrigation };
        var expense = new Expense { FarmId = farm.Id, CropPeriodId = Guid.NewGuid(), ActivityId = activity.Id, CreatedById = userId, Amount = 100m };
        var db = new MockDbContextBuilder().WithFarms(farm).WithActivities(activity).WithExpenses(expense).Build();

        await new UpdateActivityCommandHandler(db).Handle(new UpdateActivityCommand(
            activity.Id, userId, Cost: 0f, CostWasProvided: true), CancellationToken.None);

        expense.ArchivedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Updating_positive_activity_cost_should_reactivate_existing_expense_and_refresh_category_and_date()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var period = new CropPeriod { FarmId = farm.Id, CropName = "Buğday", PlantedAt = new DateOnly(2026, 1, 1) };
        var activity = new Activity { FarmId = farm.Id, Farm = farm, CropPeriodId = period.Id, CreatedById = userId, Cost = 100f, ActivityType = ActivityType.Irrigation };
        var expense = new Expense { FarmId = farm.Id, CropPeriodId = period.Id, ActivityId = activity.Id, CreatedById = userId, Amount = 100m, ArchivedAtUtc = DateTime.UtcNow };
        var occurredAt = DateTime.UtcNow.AddDays(-1);
        var db = new MockDbContextBuilder().WithFarms(farm).WithCropPeriods(period).WithActivities(activity).WithExpenses(expense).Build();

        await new UpdateActivityCommandHandler(db).Handle(new UpdateActivityCommand(
            activity.Id, userId, ActivityType: ActivityType.Spraying, OccurredAt: occurredAt, Cost: 250f, CostWasProvided: true), CancellationToken.None);

        expense.ArchivedAtUtc.Should().BeNull();
        expense.Amount.Should().Be(250m);
        expense.Category.Should().Be(ExpenseCategory.Pesticide);
        expense.OccurredAtUtc.Should().Be(occurredAt);
    }

    [Fact]
    public async Task Costed_activity_without_period_should_be_rejected()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var db = new MockDbContextBuilder().WithFarms(farm).Build();

        var act = () => new CreateActivityCommandHandler(db).Handle(new CreateActivityCommand(
            farm.Id, userId, ActivityName: "Sulama", Cost: 100f, OccurredAt: DateTime.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Archiving_activity_should_archive_linked_expense()
    {
        var userId = Guid.NewGuid();
        var farm = new Farm { OwnerId = userId, Name = "Tarla" };
        var activity = new Activity { FarmId = farm.Id, Farm = farm, CreatedById = userId, Cost = 100f };
        var expense = new Expense { FarmId = farm.Id, CropPeriodId = Guid.NewGuid(), ActivityId = activity.Id, CreatedById = userId, Amount = 100m };
        var db = new MockDbContextBuilder().WithFarms(farm).WithActivities(activity).WithExpenses(expense).Build();

        (await new ArchiveActivityCommandHandler(db).Handle(new ArchiveActivityCommand(activity.Id, userId), CancellationToken.None))
            .Should().BeTrue();

        expense.ArchivedAtUtc.Should().NotBeNull();
    }
}
