using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.IntegrationTests;

public class FarmSummaryEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public FarmSummaryEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<ApplicationDbContext> GetDbContextAsync()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private async Task<Farm> SeedFarmAsync(Guid ownerId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.FindAsync(ownerId);
        if (user == null)
        {
            db.Users.Add(new User
            {
                Id = ownerId,
                PhoneNumber = $"+90555{Random.Shared.Next(1000000, 9999999)}",
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active,
            });
        }

        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            Latitude = 38.0,
            Longitude = 35.0,
            SizeInHectares = 10.0,
            IrrigationMethod = IrrigationMethod.Drip,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        return farm;
    }

    private async Task<FarmTask> SeedTaskAsync(Guid farmId, string title, DateOnly dueDate, TaskStatus status, DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var task = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = title,
            Description = $"{title} Açıklaması",
            Reason = "Zamanı geldi",
            Priority = TaskPriority.Medium,
            Status = status,
            DueDate = dueDate,
            DedupeKey = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = createdAt ?? DateTime.UtcNow,
        };

        db.FarmTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    private async Task<Activity> SeedActivityAsync(Guid farmId, ActivityType type, string desc, DateTime occurredAtUtc, ActivityStatus status = ActivityStatus.Confirmed, DateTime? archivedAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityType = type,
            Description = desc,
            OccurredAtUtc = occurredAtUtc,
            Status = status,
            ArchivedAtUtc = archivedAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return activity;
    }

    [Fact]
    public async Task GetSummary_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/farms/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSummary_WhenUserHasNoFarms_Returns200WithEmptyLists()
    {
        var userId = Guid.NewGuid();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.Farms.Should().BeEmpty();
        summary.UpcomingTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummary_SingleFarm_ReturnsCorrectFarmDtoAndNullWorkDetailsWhenEmpty()
    {
        var userId = Guid.NewGuid();
        var farm = await SeedFarmAsync(userId, "Batı Bahçesi");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.Farms.Should().HaveCount(1);
        summary.Farms[0].Farm.Id.Should().Be(farm.Id);
        summary.Farms[0].Farm.Name.Should().Be("Batı Bahçesi");
        summary.Farms[0].NextTask.Should().BeNull();
        summary.Farms[0].LastActivity.Should().BeNull();
        summary.UpcomingTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummary_NextTask_PicksEarliestOpenTaskAndIgnoresCompletedOrCancelled()
    {
        var userId = Guid.NewGuid();
        var farm = await SeedFarmAsync(userId, "Görevli Tarla");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);

        // Cancelled and Completed tasks (should be ignored)
        await SeedTaskAsync(farm.Id, "İptal Görev", yesterday, TaskStatus.Cancelled);
        await SeedTaskAsync(farm.Id, "Tamamlanan Görev", yesterday, TaskStatus.Completed);
        await SeedTaskAsync(farm.Id, "Uygulanmayan Görev", yesterday, TaskStatus.NotApplied);

        // Open tasks: Overdue (yesterday) vs tomorrow vs nextWeek
        var overdueTask = await SeedTaskAsync(farm.Id, "Gecikmiş Sulama", yesterday, TaskStatus.Overdue);
        await SeedTaskAsync(farm.Id, "Yarınki İlaçlama", tomorrow, TaskStatus.Planned);
        await SeedTaskAsync(farm.Id, "Haftaya Hasat", nextWeek, TaskStatus.New);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.Farms.Should().HaveCount(1);
        summary.Farms[0].NextTask.Should().NotBeNull();
        summary.Farms[0].NextTask!.Id.Should().Be(overdueTask.Id);
        summary.Farms[0].NextTask!.Title.Should().Be("Gecikmiş Sulama");
    }

    [Fact]
    public async Task GetSummary_LastActivity_PicksNewestOccurredAtAndIgnoresArchived()
    {
        var userId = Guid.NewGuid();
        var farm = await SeedFarmAsync(userId, "Faaliyetli Tarla");

        var now = DateTime.UtcNow;
        var older = now.AddDays(-5);
        var newer = now.AddDays(-1);
        var newestArchived = now; // even newer, but archived!

        await SeedActivityAsync(farm.Id, ActivityType.Fertilization, "Eski Gübreleme", older);
        var validNewest = await SeedActivityAsync(farm.Id, ActivityType.Spraying, "Geçerli Son İlaçlama", newer);
        await SeedActivityAsync(farm.Id, ActivityType.Irrigation, "Arşivli Sulama", newestArchived, ActivityStatus.Confirmed, archivedAtUtc: now);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.Farms.Should().HaveCount(1);
        summary.Farms[0].LastActivity.Should().NotBeNull();
        summary.Farms[0].LastActivity!.Id.Should().Be(validNewest.Id);
        summary.Farms[0].LastActivity!.Description.Should().Be("Geçerli Son İlaçlama");
    }

    [Fact]
    public async Task GetSummary_UpcomingTasks_ReturnsGlobalSortedTasksAndPreservesMultipleTasksPerFarm()
    {
        var userId = Guid.NewGuid();
        var farm1 = await SeedFarmAsync(userId, "Tarla 1");
        var farm2 = await SeedFarmAsync(userId, "Tarla 2");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Farm 1 has 2 tasks: today & in 3 days
        var t1 = await SeedTaskAsync(farm1.Id, "Tarla 1 Bugün", today, TaskStatus.New);
        var t3 = await SeedTaskAsync(farm1.Id, "Tarla 1 3 Gün Sonra", today.AddDays(3), TaskStatus.Planned);

        // Farm 2 has 1 task: tomorrow
        var t2 = await SeedTaskAsync(farm2.Id, "Tarla 2 Yarın", today.AddDays(1), TaskStatus.Planned);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();

        // 1. Both tasks from Farm 1 are in upcomingTasks
        summary!.UpcomingTasks.Should().HaveCount(3);
        summary.UpcomingTasks[0].Id.Should().Be(t1.Id);
        summary.UpcomingTasks[1].Id.Should().Be(t2.Id);
        summary.UpcomingTasks[2].Id.Should().Be(t3.Id);

        // 2. Each farm has its own nextTask
        var f1Summary = summary.Farms.Single(f => f.Farm.Id == farm1.Id);
        f1Summary.NextTask!.Id.Should().Be(t1.Id);

        var f2Summary = summary.Farms.Single(f => f.Farm.Id == farm2.Id);
        f2Summary.NextTask!.Id.Should().Be(t2.Id);
    }

    [Fact]
    public async Task GetSummary_UpcomingLimit_RestrictsUpcomingTasksCount()
    {
        var userId = Guid.NewGuid();
        var farm = await SeedFarmAsync(userId, "Çok Görevli Tarla");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (int i = 1; i <= 6; i++)
        {
            await SeedTaskAsync(farm.Id, $"Görev {i}", today.AddDays(i), TaskStatus.New);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary?upcomingLimit=2");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.UpcomingTasks.Should().HaveCount(2);
        summary.UpcomingTasks[0].Title.Should().Be("Görev 1");
        summary.UpcomingTasks[1].Title.Should().Be("Görev 2");
    }

    [Fact]
    public async Task GetSummary_CrossTenant_UserACannotSeeUserBData()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var farmA = await SeedFarmAsync(userA, "User A Özel Tarla");
        var farmB = await SeedFarmAsync(userB, "User B Özel Tarla");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var taskA = await SeedTaskAsync(farmA.Id, "User A Görevi", today, TaskStatus.New);
        var taskB = await SeedTaskAsync(farmB.Id, "User B Görevi", today, TaskStatus.New);

        var actA = await SeedActivityAsync(farmA.Id, ActivityType.Irrigation, "User A Faaliyeti", DateTime.UtcNow);
        var actB = await SeedActivityAsync(farmB.Id, ActivityType.Fertilization, "User B Faaliyeti", DateTime.UtcNow);

        // Query as User A
        using var reqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        reqA.Headers.Add("X-User-Id", userA.ToString());
        reqA.Headers.Add("X-User-Role", "Farmer");

        var responseA = await _client.SendAsync(reqA);
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryA = await responseA.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summaryA.Should().NotBeNull();

        // User A must ONLY see Farm A
        summaryA!.Farms.Should().ContainSingle(f => f.Farm.Id == farmA.Id);
        summaryA.Farms.Should().NotContain(f => f.Farm.Id == farmB.Id);

        // Next task and activity must be for Farm A only
        summaryA.Farms[0].NextTask!.Id.Should().Be(taskA.Id);
        summaryA.Farms[0].LastActivity!.Id.Should().Be(actA.Id);

        // Upcoming tasks must contain only User A tasks
        summaryA.UpcomingTasks.Should().ContainSingle(t => t.Id == taskA.Id);
        summaryA.UpcomingTasks.Should().NotContain(t => t.Id == taskB.Id);
    }

    [Fact]
    public async Task GetSummary_HighVolume_ReturnsSingleNextTaskAndLastActivityPerFarm()
    {
        var farmerId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var farm1 = await SeedFarmAsync(farmerId, "Büyük Tarla 1");
        var farm2 = await SeedFarmAsync(farmerId, "Büyük Tarla 2");

        // Seed 20 historical activities for Farm 1 and 20 for Farm 2
        for (int i = 0; i < 20; i++)
        {
            await SeedActivityAsync(farm1.Id, ActivityType.Irrigation, $"Eski Sulama {i}", DateTime.UtcNow.AddDays(-30 + i));
            await SeedActivityAsync(farm2.Id, ActivityType.Fertilization, $"Eski Gübreleme {i}", DateTime.UtcNow.AddDays(-30 + i));
        }

        // Latest activity for farm 1
        var latestAct1 = await SeedActivityAsync(farm1.Id, ActivityType.Spraying, "En Yeni İlaçlama", DateTime.UtcNow.AddHours(-1));
        // Latest activity for farm 2
        var latestAct2 = await SeedActivityAsync(farm2.Id, ActivityType.Harvest, "En Yeni Hasat", DateTime.UtcNow.AddHours(-2));

        // Seed 10 open tasks for Farm 1
        var earliestTask1 = await SeedTaskAsync(farm1.Id, "İlk Görev Farm 1", today.AddDays(1), TaskStatus.New);
        for (int t = 2; t <= 10; t++)
        {
            await SeedTaskAsync(farm1.Id, $"Görev {t} Farm 1", today.AddDays(t), TaskStatus.Planned);
        }

        // Seed 10 open tasks for Farm 2
        var earliestTask2 = await SeedTaskAsync(farm2.Id, "İlk Görev Farm 2", today.AddDays(2), TaskStatus.New);
        for (int t = 3; t <= 11; t++)
        {
            await SeedTaskAsync(farm2.Id, $"Görev {t} Farm 2", today.AddDays(t), TaskStatus.Planned);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary?upcomingLimit=5");
        req.Headers.Add("X-User-Id", farmerId.ToString());
        req.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summary.Should().NotBeNull();
        summary!.Farms.Should().HaveCount(2);

        var farmSummary1 = summary.Farms.First(f => f.Farm.Id == farm1.Id);
        farmSummary1.NextTask!.Id.Should().Be(earliestTask1.Id);
        farmSummary1.LastActivity!.Id.Should().Be(latestAct1.Id);

        var farmSummary2 = summary.Farms.First(f => f.Farm.Id == farm2.Id);
        farmSummary2.NextTask!.Id.Should().Be(earliestTask2.Id);
        farmSummary2.LastActivity!.Id.Should().Be(latestAct2.Id);

        summary.UpcomingTasks.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetSummary_DoesNotCollideWithGetFarmByIdRoute()
    {
        var farmerId = Guid.NewGuid();
        var farm = await SeedFarmAsync(farmerId, "Doğrulama Tarlası");

        // Request 1: /api/v1/farms/summary -> calls summary endpoint, NOT GetFarmById
        using var summaryReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms/summary");
        summaryReq.Headers.Add("X-User-Id", farmerId.ToString());
        summaryReq.Headers.Add("X-User-Role", "Farmer");

        var summaryRes = await _client.SendAsync(summaryReq);
        summaryRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryData = await summaryRes.Content.ReadFromJsonAsync<FarmSummaryResponse>(CustomWebApplicationFactory.JsonOptions);
        summaryData.Should().NotBeNull();
        summaryData!.Farms.Should().ContainSingle(f => f.Farm.Id == farm.Id);

        // Request 2: /api/v1/farms/{farm.Id} -> calls GetFarmById
        using var getByIdReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farm.Id}");
        getByIdReq.Headers.Add("X-User-Id", farmerId.ToString());
        getByIdReq.Headers.Add("X-User-Role", "Farmer");

        var getByIdRes = await _client.SendAsync(getByIdReq);
        getByIdRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var singleFarm = await getByIdRes.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        singleFarm.Should().NotBeNull();
        singleFarm!.Id.Should().Be(farm.Id);
    }
}
