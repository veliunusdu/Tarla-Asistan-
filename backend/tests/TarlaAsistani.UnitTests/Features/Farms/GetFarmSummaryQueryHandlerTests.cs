using FluentAssertions;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.Farms;

[Trait("Category", "Farms")]
public class GetFarmSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenNoFarmsExist_ReturnsEmptyResponse()
    {
        var db = new MockDbContextBuilder().Build();
        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(Guid.NewGuid(), UserRole.Farmer);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Farms.Should().BeEmpty();
        result.UpcomingTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenFarmHasTasksAndActivities_MapsNextTaskAndLastActivityCorrectly()
    {
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Bereket Tarlası",
            CreatedAtUtc = DateTime.UtcNow
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var t1 = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Geciken Sulama",
            Description = "Sulama",
            Reason = "Kuraklık",
            Status = TaskStatus.Overdue,
            DueDate = today.AddDays(-1),
            DedupeKey = "k1",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        var t2 = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Yarınki İlaçlama",
            Description = "İlaçlama",
            Reason = "Zararlı",
            Status = TaskStatus.Planned,
            DueDate = today.AddDays(1),
            DedupeKey = "k2",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        var completedTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Eski İş",
            Description = "Yapıldı",
            Reason = "Rutin",
            Status = TaskStatus.Completed,
            DueDate = today.AddDays(-5),
            DedupeKey = "k3",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-6)
        };

        var oldAct = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Irrigation,
            Description = "Eski Sulama",
            OccurredAtUtc = DateTime.UtcNow.AddDays(-10),
            Status = ActivityStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        var newAct = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Fertilization,
            Description = "Yeni Gübreleme",
            OccurredAtUtc = DateTime.UtcNow.AddDays(-1),
            Status = ActivityStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(t1, t2, completedTask)
            .WithActivities(oldAct, newAct)
            .Build();

        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(farmerId, UserRole.Farmer, UpcomingLimit: 5);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Farms.Should().HaveCount(1);

        var summary = result.Farms[0];
        summary.Farm.Id.Should().Be(farmId);
        summary.Farm.Name.Should().Be("Bereket Tarlası");

        // Next task should be the earliest open task (t1: Overdue yesterday)
        summary.NextTask.Should().NotBeNull();
        summary.NextTask!.Id.Should().Be(t1.Id);
        summary.NextTask.Title.Should().Be("Geciken Sulama");

        // Last activity should be newAct
        summary.LastActivity.Should().NotBeNull();
        summary.LastActivity!.Id.Should().Be(newAct.Id);
        summary.LastActivity.Description.Should().Be("Yeni Gübreleme");

        // Upcoming tasks should contain both open tasks ordered by due date (t1, then t2)
        result.UpcomingTasks.Should().HaveCount(2);
        result.UpcomingTasks[0].Id.Should().Be(t1.Id);
        result.UpcomingTasks[1].Id.Should().Be(t2.Id);
    }

    [Fact]
    public async Task Handle_WhenMultipleActivitiesHaveSameOccurredAt_UsesCreatedAtTieBreakAndExcludesDraftOrArchived()
    {
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Tarla 1",
            CreatedAtUtc = DateTime.UtcNow
        };

        var sameOccurredAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        // Activity A: OccurredAt = 10:00, CreatedAt = 10:01
        var actA = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Spraying,
            Description = "Activity A",
            OccurredAtUtc = sameOccurredAt,
            CreatedAtUtc = sameOccurredAt.AddMinutes(1),
            Status = ActivityStatus.Confirmed
        };

        // Activity B: OccurredAt = 10:00, CreatedAt = 10:05 (Should win tie-break!)
        var actB = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Spraying,
            Description = "Activity B",
            OccurredAtUtc = sameOccurredAt,
            CreatedAtUtc = sameOccurredAt.AddMinutes(5),
            Status = ActivityStatus.Confirmed
        };

        // Activity C: newer OccurredAt, but Archived! (Must be ignored)
        var actArchived = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Harvest,
            Description = "Archived Activity",
            OccurredAtUtc = sameOccurredAt.AddDays(1),
            CreatedAtUtc = sameOccurredAt.AddDays(1),
            Status = ActivityStatus.Confirmed,
            ArchivedAtUtc = DateTime.UtcNow
        };

        // Activity D: newer OccurredAt, but Draft! (Must be ignored)
        var actDraft = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = ActivityType.Harvest,
            Description = "Draft Activity",
            OccurredAtUtc = sameOccurredAt.AddDays(2),
            CreatedAtUtc = sameOccurredAt.AddDays(2),
            Status = ActivityStatus.Draft
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithActivities(actA, actB, actArchived, actDraft)
            .Build();

        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(farmerId, UserRole.Farmer);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Farms.Should().ContainSingle();
        var lastActivity = result.Farms[0].LastActivity;
        lastActivity.Should().NotBeNull();
        lastActivity!.Id.Should().Be(actB.Id);
        lastActivity.Description.Should().Be("Activity B");
    }

    [Fact]
    public async Task Handle_WhenMultipleTasksExist_SelectsEarliestOpenAndExcludesCompletedAndCancelled()
    {
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Tarla 1",
            CreatedAtUtc = DateTime.UtcNow
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Older but completed (Must NOT be next task)
        var completedTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Tamamlanan İş",
            Description = "Yapıldı",
            Reason = "Rutin",
            Status = TaskStatus.Completed,
            DueDate = today.AddDays(-10),
            DedupeKey = "k1",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        // Older but cancelled (Must NOT be next task)
        var cancelledTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "İptal Edilen İş",
            Description = "İptal",
            Reason = "Vazgeçildi",
            Status = TaskStatus.Cancelled,
            DueDate = today.AddDays(-8),
            DedupeKey = "k2",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8)
        };

        // Overdue open (Should be the winner for NextTask)
        var overdueOpenTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Geciken Açık İş",
            Description = "Hemen yapılmalı",
            Reason = "Gecikti",
            Status = TaskStatus.Overdue,
            DueDate = today.AddDays(-2),
            DedupeKey = "k3",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        // Tomorrow open
        var tomorrowOpenTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Yarınki İş",
            Description = "Yarın",
            Reason = "Plan",
            Status = TaskStatus.Planned,
            DueDate = today.AddDays(1),
            DedupeKey = "k4",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(completedTask, cancelledTask, overdueOpenTask, tomorrowOpenTask)
            .Build();

        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(farmerId, UserRole.Farmer);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Farms.Should().ContainSingle();
        var nextTask = result.Farms[0].NextTask;
        nextTask.Should().NotBeNull();
        nextTask!.Id.Should().Be(overdueOpenTask.Id);
        nextTask.Title.Should().Be("Geciken Açık İş");
    }

    [Fact]
    public async Task Handle_WhenMultipleFarmsHaveTasks_UpcomingTasksIsGlobalAndNotLimitedToOnePerFarm()
    {
        var farmerId = Guid.NewGuid();
        var farm1Id = Guid.NewGuid();
        var farm2Id = Guid.NewGuid();

        var farm1 = new Farm { Id = farm1Id, OwnerId = farmerId, Name = "Tarla 1", CreatedAtUtc = DateTime.UtcNow };
        var farm2 = new Farm { Id = farm2Id, OwnerId = farmerId, Name = "Tarla 2", CreatedAtUtc = DateTime.UtcNow };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Farm 1 has Task A (today) and Task B (tomorrow)
        var taskA = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farm1Id,
            Title = "Task A (Bugün)",
            Description = "Tarla 1 ilk iş",
            Reason = "Rutin",
            Status = TaskStatus.New,
            DueDate = today,
            DedupeKey = "tA",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        var taskB = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farm1Id,
            Title = "Task B (Yarın)",
            Description = "Tarla 1 ikinci iş",
            Reason = "Rutin",
            Status = TaskStatus.Planned,
            DueDate = today.AddDays(1),
            DedupeKey = "tB",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        // Farm 2 has Task C (two days later)
        var taskC = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farm2Id,
            Title = "Task C (2 gün sonra)",
            Description = "Tarla 2 işi",
            Reason = "Rutin",
            Status = TaskStatus.Planned,
            DueDate = today.AddDays(2),
            DedupeKey = "tC",
            CreatedAtUtc = DateTime.UtcNow
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm1, farm2)
            .WithFarmTasks(taskA, taskB, taskC)
            .Build();

        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(farmerId, UserRole.Farmer, UpcomingLimit: 3);

        var result = await handler.Handle(query, CancellationToken.None);

        // Both Task A and Task B from farm1 should be in upcomingTasks along with Task C from farm2
        result.UpcomingTasks.Should().HaveCount(3);
        result.UpcomingTasks.Select(t => t.Id).Should().ContainInOrder(taskA.Id, taskB.Id, taskC.Id);

        // Meanwhile nextTask per farm should show only Task A for farm 1 and Task C for farm 2
        result.Farms.First(f => f.Farm.Id == farm1Id).NextTask!.Id.Should().Be(taskA.Id);
        result.Farms.First(f => f.Farm.Id == farm2Id).NextTask!.Id.Should().Be(taskC.Id);
    }

    [Fact]
    public async Task Handle_HighVolumeFarmActivitiesAndTasks_ReturnsSingleNextTaskAndLastActivityPerFarm()
    {
        var farmerId = Guid.NewGuid();
        var farms = new List<Farm>();
        var activities = new List<Activity>();
        var tasks = new List<FarmTask>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (int i = 0; i < 10; i++)
        {
            var farmId = Guid.NewGuid();
            farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = $"Tarla {i}",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-100)
            });

            // Add 50 confirmed activities per farm
            for (int a = 0; a < 50; a++)
            {
                activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    ActivityType = ActivityType.Irrigation,
                    Description = $"Aktivite {a}",
                    OccurredAtUtc = DateTime.UtcNow.AddDays(-100 + a),
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-100 + a),
                    Status = ActivityStatus.Confirmed
                });
            }

            // Add 5 archived activities per farm
            for (int a = 0; a < 5; a++)
            {
                activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    ActivityType = ActivityType.Fertilization,
                    Description = $"Arşivli {a}",
                    OccurredAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    Status = ActivityStatus.Confirmed,
                    ArchivedAtUtc = DateTime.UtcNow
                });
            }

            // Add 10 open tasks per farm
            for (int t = 0; t < 10; t++)
            {
                tasks.Add(new FarmTask
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    Title = $"Açık Görev {t}",
                    Description = "Görev",
                    Reason = "Rutin",
                    Status = TaskStatus.Planned,
                    DueDate = today.AddDays(t + 1),
                    DedupeKey = $"t_{i}_{t}",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
                });
            }

            // Add 10 completed tasks per farm
            for (int t = 0; t < 10; t++)
            {
                tasks.Add(new FarmTask
                {
                    Id = Guid.NewGuid(),
                    FarmId = farmId,
                    Title = $"Biten Görev {t}",
                    Description = "Bitti",
                    Reason = "Rutin",
                    Status = TaskStatus.Completed,
                    DueDate = today.AddDays(-t - 1),
                    DedupeKey = $"done_{i}_{t}",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-20)
                });
            }
        }

        var db = new MockDbContextBuilder()
            .WithFarms(farms.ToArray())
            .WithActivities(activities.ToArray())
            .WithFarmTasks(tasks.ToArray())
            .Build();

        var handler = new GetFarmSummaryQueryHandler(db);
        var query = new GetFarmSummaryQuery(farmerId, UserRole.Farmer, UpcomingLimit: 5);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Farms.Should().HaveCount(10);
        result.UpcomingTasks.Should().HaveCount(5);

        // Every farm must have exactly 1 nextTask and 1 lastActivity
        foreach (var farmSummary in result.Farms)
        {
            farmSummary.NextTask.Should().NotBeNull();
            farmSummary.LastActivity.Should().NotBeNull();
            // LastActivity must not be archived
            farmSummary.LastActivity!.Description.Should().StartWith("Aktivite");
        }
    }
}
