using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
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
    }

    [Fact]
    public async Task ApplyAdvisoryAsync_ShouldUpdateTaskDueDateAndMarkApplied()
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
        task.DueDate.Should().Be(recommendedDate);
        task.Status.Should().Be(TaskStatus.Planned);
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
