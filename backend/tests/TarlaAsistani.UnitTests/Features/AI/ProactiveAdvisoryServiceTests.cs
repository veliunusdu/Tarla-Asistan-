using System.Text.Json;
using TarlaAsistani.Application.Features.Weather.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;
using TarlaAsistani.UnitTests.Common;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class ProactiveAdvisoryServiceTests
{
    private readonly Mock<IWeatherProvider> _mockWeather = new();
    private readonly Mock<IProactiveAdvisoryEngine> _mockEngine = new();
    private readonly Mock<IPushNotificationService> _mockPush = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    [Fact]
    public async Task EvaluateFarmAdvisoriesAsync_ShouldPersistAndDispatchNotification()
    {
        var farm = new Farm
        {
            Id = _farmId,
            OwnerId = _userId,
            Name = "Güney Tarlası",
            Latitude = 38.0,
            Longitude = 32.0,
            Owner = new User
            {
                Id = _userId,
                Profile = new Profile { FullName = "Ali Veli", NotificationsEnabled = true }
            }
        };

        var deviceToken = new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "fcm-token-1",
            Active = true
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithUsers(farm.Owner)
            .WithProfiles(farm.Owner.Profile!)
            .WithDeviceTokens(deviceToken)
            .Build();

        _mockWeather
            .Setup(w => w.GetWeatherAsync(38.0, 32.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecastData([]));

        var evalResult = new ProactiveAdvisoryEvaluationResult(
            AdvisoryType: ProactiveAdvisoryType.FertilizerDelay,
            Severity: AdvisorySeverity.Critical,
            ActionType: ProactiveActionType.PostponeTask,
            Title: "Gübre Ertele",
            Summary: "Yağmur bekleniyor",
            AgronomicExplanation: "Yıkanma riski",
            ActionRecommendation: "Yağmur sonrasına ertele",
            RecommendedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            DedupeKey: $"adv-fert-{_farmId}-test-1"
        );

        _mockEngine
            .Setup(e => e.Evaluate(It.IsAny<Farm>(), It.IsAny<IReadOnlyList<Activity>>(), It.IsAny<IReadOnlyList<FarmTask>>(), It.IsAny<WeatherForecastData>(), It.IsAny<DateTime>()))
            .Returns([evalResult]);
        Notification? dispatchedNotification = null;
        _mockPush
            .Setup(p => p.SendNotificationAsync(It.IsAny<Notification>(), "fcm-token-1", It.IsAny<CancellationToken>()))
            .Callback<Notification, string, CancellationToken>((notification, _, _) => dispatchedNotification = notification)
            .ReturnsAsync(true);

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance,
            _mockPush.Object);

        var advisories = await service.EvaluateFarmAdvisoriesAsync(_farmId);

        advisories.Should().NotBeNull();
        advisories.Should().HaveCount(1);
        advisories[0].Title.Should().Be("Gübre Ertele");

        // Verify push was called
        _mockPush.Verify(p => p.SendNotificationAsync(
            It.Is<Notification>(n => n.NotificationType == NotificationType.ProactiveAdvisory),
            "fcm-token-1",
            It.IsAny<CancellationToken>()), Times.Once);
        dispatchedNotification.Should().NotBeNull();
        dispatchedNotification!.Status.Should().Be(NotificationStatus.Sent);
        dispatchedNotification.SentAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAdvisory_WithValidTaskAndRecommendedDate_UpdatesTaskDate()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var recommendedDate = new DateOnly(2026, 6, 15);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Gübreleme",
            DueDate = new DateOnly(2026, 6, 12),
            Status = TaskStatus.New,
            DedupeKey = "task-1"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = recommendedDate,
            IsApplied = false,
            DedupeKey = "dedupe-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var result = await service.ApplyAdvisoryAsync(advisoryId, _userId);

        result.Should().BeTrue();
        advisory.IsApplied.Should().BeTrue();
        advisory.AppliedAtUtc.Should().NotBeNull();
        advisory.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        task.DueDate.Should().Be(recommendedDate);
        task.Status.Should().Be(TaskStatus.Planned);
        task.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ApplyAdvisory_WhenAlreadyApplied_ReturnsConflict()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var recommendedDate = new DateOnly(2026, 6, 15);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = new DateOnly(2026, 6, 10),
            Status = TaskStatus.Planned,
            DedupeKey = "task-applied"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = recommendedDate,
            IsApplied = true,
            AppliedAtUtc = DateTime.UtcNow.AddHours(-1),
            DedupeKey = "dedupe-applied"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*daha önce uygulanmıştır*");
    }

    [Fact]
    public async Task ApplyAdvisory_Twice_SecondRequestDoesNotMutateTask()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var recommendedDate = new DateOnly(2026, 6, 15);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = new DateOnly(2026, 6, 10),
            Status = TaskStatus.New,
            DedupeKey = "task-twice"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = recommendedDate,
            IsApplied = false,
            DedupeKey = "dedupe-twice"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        // First call succeeds
        var firstResult = await service.ApplyAdvisoryAsync(advisoryId, _userId);
        firstResult.Should().BeTrue();
        task.DueDate.Should().Be(recommendedDate);
        task.Status.Should().Be(TaskStatus.Planned);
        advisory.IsApplied.Should().BeTrue();

        var savedTaskDueDate = task.DueDate;
        var savedTaskUpdatedAt = task.UpdatedAtUtc;
        var savedAdvisoryAppliedAt = advisory.AppliedAtUtc;

        // Second call throws ConflictException and does not mutate
        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*daha önce uygulanmıştır*");

        task.DueDate.Should().Be(savedTaskDueDate);
        task.UpdatedAtUtc.Should().Be(savedTaskUpdatedAt);
        advisory.AppliedAtUtc.Should().Be(savedAdvisoryAppliedAt);
    }

    [Fact]
    public async Task ApplyWeatherAdvisory_BeforeFreshnessDeadline_Succeeds()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var recommendedDate = new DateOnly(2026, 6, 15);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = new DateOnly(2026, 6, 10),
            Status = TaskStatus.New,
            DedupeKey = "task-fresh-deadline"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = recommendedDate,
            ValidUntilUtc = DateTime.UtcNow.AddHours(2), // Valid for 2 more hours
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = "dedupe-fresh-deadline"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var result = await service.ApplyAdvisoryAsync(advisoryId, _userId);

        result.Should().BeTrue();
        task.DueDate.Should().Be(recommendedDate);
        advisory.IsApplied.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyWeatherAdvisory_AfterFreshnessDeadline_Returns409()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var originalDueDate = new DateOnly(2026, 6, 10);
        var recommendedDate = new DateOnly(2026, 6, 15);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = originalDueDate,
            Status = TaskStatus.New,
            DedupeKey = "task-expired-deadline"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = recommendedDate,
            ValidUntilUtc = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = "dedupe-expired-deadline"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*süresi doldu*");

        // Verify task DueDate unchanged and IsApplied remains false
        task.DueDate.Should().Be(originalDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ProactiveAdvisoryService_UsesWeatherFetchedAtForFreshnessDeadline()
    {
        var farm = new Farm
        {
            Id = _farmId,
            OwnerId = _userId,
            Name = "Mısır Tarlası",
            Latitude = 38.0,
            Longitude = 32.0,
            ArchivedAt = null
        };

        var taskId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = TaskStatus.Planned,
            DedupeKey = "task-spray-a"
        };

        // Weather fetched at 10:00 UTC
        var fetchedAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<WeatherPoint>
        {
            new(fetchedAt, TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 28),
            new(fetchedAt.AddDays(1), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = fetchedAt
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:StaleAfterHours"] = "4"
            })
            .Build();

        // Weather provider fails, so it must fall back to snapshot with fetchedAt = 10:00 UTC
        _mockWeather
            .Setup(w => w.GetWeatherAsync(38.0, 32.0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider unavailable"));

        var evalResult = new ProactiveAdvisoryEvaluationResult(
            AdvisoryType: ProactiveAdvisoryType.SprayingWindow,
            Severity: AdvisorySeverity.Warning,
            ActionType: ProactiveActionType.PostponeTask,
            Title: "Rüzgar Riski",
            Summary: "Rüzgar yüksek",
            AgronomicExplanation: "Sürüklenme",
            ActionRecommendation: "Ertele",
            RecommendedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DedupeKey: $"adv-spray-{_farmId}-{taskId}",
            RelatedTaskId: taskId
        );

        _mockEngine
            .Setup(e => e.Evaluate(It.IsAny<Farm>(), It.IsAny<IReadOnlyList<Activity>>(), It.IsAny<IReadOnlyList<FarmTask>>(), It.IsAny<WeatherForecastData>(), It.IsAny<DateTime>()))
            .Returns([evalResult]);

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance,
            configuration: config);

        await service.EvaluateFarmAdvisoriesAsync(_farmId);

        var persisted = await db.ProactiveAdvisories.FirstOrDefaultAsync(a => a.RelatedTaskId == taskId);
        persisted.Should().NotBeNull();
        persisted!.ValidUntilUtc.Should().NotBeNull();

        // 10:00 + 4h = 14:00 UTC. It must NOT be evaluated from nowUtc (e.g. 16:30)
        var expectedDeadline = fetchedAt.AddHours(4);
        persisted.ValidUntilUtc.Should().Be(expectedDeadline);
    }

    [Fact]
    public void ListTasksAndProactiveService_ProduceSameFreshnessDeadline()
    {
        var fetchedAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var evalTime = new DateTime(2026, 9, 6, 12, 30, 0, DateTimeKind.Utc);
        var staleHours = 4;
        var defaultValidUntil = evalTime.AddDays(4);

        var serviceCalculated = WeatherDefaults.CalculateWeatherAdvisoryValidUntil(
            fetchedAt,
            defaultValidUntil,
            staleHours,
            evalTime);

        var listQueryCalculated = WeatherDefaults.CalculateWeatherAdvisoryValidUntil(
            fetchedAt,
            defaultValidUntil,
            staleHours,
            evalTime);

        serviceCalculated.Should().Be(listQueryCalculated);
        serviceCalculated.Should().Be(new DateTime(2026, 9, 6, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task WeatherFetchedAtAlreadyStale_DoesNotCreateActionablePostponeAdvisory()
    {
        var farm = new Farm
        {
            Id = _farmId,
            OwnerId = _userId,
            Name = "Mısır Tarlası",
            Latitude = 38.0,
            Longitude = 32.0,
            ArchivedAt = null
        };

        var taskId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = TaskStatus.Planned,
            DedupeKey = "task-spray-stale"
        };

        // Weather was fetched 5 hours ago (threshold is 4 hours)
        var now = DateTime.UtcNow;
        var fetchedAt = now.AddHours(-5);
        var points = new List<WeatherPoint>
        {
            new(now, TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 28)
        };

        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = fetchedAt
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:StaleAfterHours"] = "4"
            })
            .Build();

        _mockWeather
            .Setup(w => w.GetWeatherAsync(38.0, 32.0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider offline"));

        var evalResult = new ProactiveAdvisoryEvaluationResult(
            AdvisoryType: ProactiveAdvisoryType.SprayingWindow,
            Severity: AdvisorySeverity.Warning,
            ActionType: ProactiveActionType.PostponeTask,
            Title: "Rüzgar Riski",
            Summary: "Rüzgar yüksek",
            AgronomicExplanation: "Sürüklenme",
            ActionRecommendation: "Ertele",
            RecommendedDate: DateOnly.FromDateTime(now.AddDays(1)),
            DedupeKey: $"adv-spray-stale-{_farmId}-{taskId}",
            RelatedTaskId: taskId
        );

        _mockEngine
            .Setup(e => e.Evaluate(It.IsAny<Farm>(), It.IsAny<IReadOnlyList<Activity>>(), It.IsAny<IReadOnlyList<FarmTask>>(), It.IsAny<WeatherForecastData>(), It.IsAny<DateTime>()))
            .Returns([evalResult]);

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance,
            configuration: config);

        var advisories = await service.EvaluateFarmAdvisoriesAsync(_farmId);

        // Not returned as active because it's already expired
        advisories.Should().NotContain(a => a.RelatedTaskId == taskId);

        // Attempting to apply it must fail with Conflict
        var persisted = await db.ProactiveAdvisories.FirstOrDefaultAsync(a => a.RelatedTaskId == taskId);
        if (persisted != null)
        {
            persisted.ValidUntilUtc.Should().BeOnOrBefore(now);
            var act = async () => await service.ApplyAdvisoryAsync(persisted.Id, _userId);
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage("*süresi doldu*");
        }
    }

    [Fact]
    public void WeatherFetchedTimestampMissing_FailsSafe()
    {
        var now = DateTime.UtcNow;
        var defaultValidUntil = now.AddDays(4);

        var result = WeatherDefaults.CalculateWeatherAdvisoryValidUntil(
            weatherFetchedAtUtc: null,
            defaultValidUntilUtc: defaultValidUntil,
            staleAfterHours: 4,
            nowUtc: now);

        // When timestamp is missing, it must not grant future validity
        result.Should().Be(now);
    }

    [Fact]
    public void CustomStaleAfterHours_IsRespected()
    {
        var fetchedAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 9, 6, 11, 0, 0, DateTimeKind.Utc);
        var configuredHours = 2;

        var result = WeatherDefaults.CalculateWeatherAdvisoryValidUntil(
            weatherFetchedAtUtc: fetchedAt,
            defaultValidUntilUtc: now.AddDays(4),
            staleAfterHours: configuredHours,
            nowUtc: now);

        // 10:00 + 2h = 12:00 UTC
        result.Should().Be(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ApplyNonWeatherAdvisory_WhenExpired_ReturnsGenericConflictMessage()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Genel Gözlem",
            DueDate = new DateOnly(2026, 6, 10),
            Status = TaskStatus.New,
            DedupeKey = "task-obs-1"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            ActionType = ProactiveActionType.FieldScouting,
            AdvisoryType = ProactiveAdvisoryType.FrostAlert,
            RecommendedDate = new DateOnly(2026, 6, 11),
            ValidUntilUtc = DateTime.UtcNow.AddHours(-1), // Expired
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = "adv-obs-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Bu tavsiyenin geçerlilik süresi doldu.");
    }

    [Fact]
    public async Task ApplyAdvisory_WhenDismissed_IsRejected()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var initialDueDate = new DateOnly(2026, 6, 10);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Sulama",
            DueDate = initialDueDate,
            Status = TaskStatus.Planned,
            DedupeKey = "task-dismissed"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = new DateOnly(2026, 6, 14),
            IsApplied = false,
            IsDismissed = true,
            DismissedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            DedupeKey = "dedupe-dismissed"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Kapatılmış*");

        task.DueDate.Should().Be(initialDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenRelatedTaskMissing_IsRejected()
    {
        var advisoryId = Guid.NewGuid();

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = null,
            RelatedTask = null,
            RecommendedDate = new DateOnly(2026, 6, 14),
            IsApplied = false,
            DedupeKey = "dedupe-no-task"
        };

        var db = new MockDbContextBuilder()
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*bağlı bir görev bulunamadı*");

        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenRecommendedDateMissing_IsRejected()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var initialDueDate = new DateOnly(2026, 6, 10);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Çapalama",
            DueDate = initialDueDate,
            Status = TaskStatus.Planned,
            DedupeKey = "task-no-rec-date"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = null,
            IsApplied = false,
            DedupeKey = "dedupe-no-rec-date"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*önerilen bir alternatif tarih bulunamadı*");

        task.DueDate.Should().Be(initialDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenTaskCompleted_IsRejected()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var initialDueDate = new DateOnly(2026, 6, 10);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = initialDueDate,
            Status = TaskStatus.Completed,
            DedupeKey = "task-completed"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = new DateOnly(2026, 6, 14),
            IsApplied = false,
            DedupeKey = "dedupe-task-completed"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Sonlandırılmış bir görev üzerinde tavsiye uygulanamaz*");

        task.Status.Should().Be(TaskStatus.Completed);
        task.DueDate.Should().Be(initialDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenTaskNotApplied_IsRejected()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var initialDueDate = new DateOnly(2026, 6, 10);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = initialDueDate,
            Status = TaskStatus.NotApplied,
            DedupeKey = "task-not-applied"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = new DateOnly(2026, 6, 14),
            IsApplied = false,
            DedupeKey = "dedupe-task-not-applied"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Sonlandırılmış bir görev üzerinde tavsiye uygulanamaz*");

        task.Status.Should().Be(TaskStatus.NotApplied);
        task.DueDate.Should().Be(initialDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenTaskCancelled_IsRejected()
    {
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var initialDueDate = new DateOnly(2026, 6, 10);

        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama",
            DueDate = initialDueDate,
            Status = TaskStatus.Cancelled,
            DedupeKey = "task-cancelled"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            RelatedTaskId = taskId,
            RelatedTask = task,
            RecommendedDate = new DateOnly(2026, 6, 14),
            IsApplied = false,
            DedupeKey = "dedupe-task-cancelled"
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var act = async () => await service.ApplyAdvisoryAsync(advisoryId, _userId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Sonlandırılmış bir görev üzerinde tavsiye uygulanamaz*");

        task.Status.Should().Be(TaskStatus.Cancelled);
        task.DueDate.Should().Be(initialDueDate);
        advisory.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAdvisory_WhenAdvisoryNotFound_ReturnsFalse()
    {
        var db = new MockDbContextBuilder().Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var result = await service.ApplyAdvisoryAsync(Guid.NewGuid(), _userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DismissAdvisoryAsync_ShouldMarkDismissed()
    {
        var advisoryId = Guid.NewGuid();
        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _userId,
            IsDismissed = false,
            DedupeKey = "dedupe-2"
        };

        var db = new MockDbContextBuilder()
            .WithProactiveAdvisories(advisory)
            .Build();

        var service = new ProactiveAdvisoryService(
            db,
            _mockWeather.Object,
            _mockEngine.Object,
            NullLogger<ProactiveAdvisoryService>.Instance);

        var result = await service.DismissAdvisoryAsync(advisoryId, _userId);

        result.Should().BeTrue();
        advisory.IsDismissed.Should().BeTrue();
        advisory.DismissedAtUtc.Should().NotBeNull();
    }
}
