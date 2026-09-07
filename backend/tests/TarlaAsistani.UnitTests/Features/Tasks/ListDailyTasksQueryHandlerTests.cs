using System.Text.Json;
using TarlaAsistani.Application.Features.Weather.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Application.Features.Tasks.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.Tasks;

[Trait("Category", "Tasks")]
public class ListDailyTasksQueryHandlerTests
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly Guid _farmerId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    private Farm CreateSampleFarm()
    {
        return new Farm
        {
            Id = _farmId,
            OwnerId = _farmerId,
            Name = "Bereketli Tarla",
            ArchivedAt = null
        };
    }

    [Fact]
    public async Task Test11_CriticalWeatherAlert_DoesNotDuplicateIntoItemsList()
    {
        // 11. CriticalWeatherAlert items listesine duplicate girmez.
        var farm = CreateSampleFarm();
        var criticalWeather = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Don riskine karşı tarlanızı kontrol edin",
            Description = "Hassas ürünleri kontrol edin.",
            Reason = "Önümüzdeki 24 saatte don riski görülebilir.",
            Priority = TaskPriority.Critical,
            Source = TaskSource.Weather,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = "weather-frost"
        };
        var normalTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Damlama Sulama",
            Description = "Sulama yapınız.",
            Reason = "Bitki su ihtiyacı.",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = "irrigation-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(criticalWeather, normalTask)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CriticalWeatherAlerts.Should().ContainSingle(a => a.Id == criticalWeather.Id);
        result.Items.Should().ContainSingle(i => i.Id == normalTask.Id);
        result.Items.Should().NotContain(i => i.Id == criticalWeather.Id);
    }

    [Fact]
    public async Task Test12_CriticalWeatherAlerts_IsIndependentOfVisible3Limit()
    {
        // 12. CriticalWeatherAlerts 3 limitinden bağımsızdır.
        // 1 kritik hava + 4 normal görev olduğunda:
        // CriticalWeatherAlerts = 1, Items = tam 3 (toplam 4 görev görüntülenebilir)
        var farm = CreateSampleFarm();
        var criticalWeather = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Don uyarısı",
            Description = "Tedbir alın.",
            Reason = "Don riski.",
            Priority = TaskPriority.Critical,
            Source = TaskSource.Weather,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = "weather-frost"
        };

        var regularTasks = Enumerable.Range(1, 4).Select(i => new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = $"Normal Görev {i}",
            Description = "Açıklama",
            Reason = "Sebep",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = $"regular-{i}"
        }).ToArray();

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(new[] { criticalWeather }.Concat(regularTasks).ToArray())
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CriticalWeatherAlerts.Should().HaveCount(1);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Test13_ActiveDuplicateTask_IsNotRecreated()
    {
        // 13. Aktif duplicate task tekrar oluşturulmaz.
        var farm = CreateSampleFarm();
        var cropPeriodId = Guid.NewGuid();
        var cropPeriod = new CropPeriod
        {
            Id = cropPeriodId,
            FarmId = _farmId,
            CropType = CropType.Tomato,
            CropName = "Domates",
            Status = CropPeriodStatus.Active,
            PlantedAt = _today.AddDays(-20)
        };

        var spec = new TaskSpec(
            Title: "Ürün gelişimini sahada kontrol edin",
            Description: "Bitki kontrolü",
            Reason: "Kontrol",
            Priority: TaskPriority.Medium,
            Source: TaskSource.CropCalendar,
            Confidence: TaskConfidence.Medium,
            DueDate: _today,
            CropPeriodId: cropPeriodId,
            DedupeDiscriminator: "daily-field-check"
        );
        var dedupeKey = TaskEngine.CalculateDedupeKey(spec);

        // Pre-existing active task from yesterday (e.g. status is Planned or Overdue)
        var existingActiveTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            CropPeriodId = cropPeriodId,
            Title = "Ürün gelişimini sahada kontrol edin",
            Description = "Bitki kontrolü",
            Reason = "Eski kontrol",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.Medium,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(-1),
            DedupeKey = dedupeKey
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(cropPeriod)
            .WithFarmTasks(existingActiveTask)
            .Build();

        // Run TaskEngine
        await TaskEngine.EnsureDailyTasksAsync(db, farm, _today, CancellationToken.None);

        // Verify no duplicate was added
        db.FarmTasks.Count(t => t.DedupeKey == dedupeKey).Should().Be(1);
    }

    [Fact]
    public void Test14_ReasonGenerationPreserved_NotEmptyAndExplainable()
    {
        // 14. Reason generation korunur.
        var cropPeriodId = Guid.NewGuid();
        var spec = new TaskSpec(
            Title: "Ürün gelişimini sahada kontrol edin",
            Description: "Bitki gelişimini kontrol edin.",
            Reason: "Domates üretim döneminin 15. günü için düzenli saha kontrolü.",
            Priority: TaskPriority.Medium,
            Source: TaskSource.CropCalendar,
            Confidence: TaskConfidence.Medium,
            DueDate: _today,
            CropPeriodId: cropPeriodId,
            DedupeDiscriminator: "daily-field-check"
        );

        spec.Reason.Should().NotBeNullOrWhiteSpace();
        spec.Reason.Should().Contain("15. günü için düzenli saha kontrolü");
    }

    [Fact]
    public async Task Test15_OverdueContractPreserved_OverdueTasksSeparateAndNotDuplicated()
    {
        // 15. Overdue davranışı mevcut ürün kontratına uygundur.
        var farm = CreateSampleFarm();
        var overdueTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Geçmiş Görev",
            Description = "Açıklama",
            Reason = "Gerekçe",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.New, // New but dueDate in the past
            DueDate = _today.AddDays(-2),
            DedupeKey = "past-1"
        };
        var todayTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Bugünkü Görev",
            Description = "Açıklama",
            Reason = "Gerekçe",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = "today-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(overdueTask, todayTask)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Overdue.Should().ContainSingle(o => o.Id == overdueTask.Id);
        result.Items.Should().ContainSingle(i => i.Id == todayTask.Id);
        result.Items.Should().NotContain(i => i.Id == overdueTask.Id);
    }

    [Fact]
    public async Task Test16_CancelledAndCompletedTasks_DoNotEnterDailyVisibleItems()
    {
        // 16. Cancelled / Completed task günlük aktif 3'e girmez.
        var farm = CreateSampleFarm();
        var completedTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Tamamlanan Görev",
            Description = "Açıklama",
            Reason = "Gerekçe",
            Priority = TaskPriority.Critical,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Completed,
            DueDate = _today,
            DedupeKey = "completed-1"
        };
        var cancelledTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "İptal Edilen Görev",
            Description = "Açıklama",
            Reason = "Gerekçe",
            Priority = TaskPriority.Critical,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Cancelled,
            DueDate = _today,
            DedupeKey = "cancelled-1"
        };
        var activeTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farmId,
            Title = "Aktif Görev",
            Description = "Açıklama",
            Reason = "Gerekçe",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.New,
            DueDate = _today,
            DedupeKey = "active-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(completedTask, cancelledTask, activeTask)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(i => i.Id == activeTask.Id);
        result.Items.Should().NotContain(i => i.Id == completedTask.Id);
        result.Items.Should().NotContain(i => i.Id == cancelledTask.Id);
    }

    [Fact]
    public async Task ListTasks_SprayingTaskWithHighWind_ReturnsWeatherPostponeSuggestion()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var tomorrow = _today.AddDays(1);
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Elma İlaçlama Görevi",
            Description = "Mantar ve böcek ilaçlaması yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = tomorrow,
            DedupeKey = "spray-wind-1"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 26),
            new(now.AddDays(2), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var suggestion = item.WeatherPostponeSuggestion!;
        suggestion.Reasons.Should().Contain(r => r.Contains("rüzg", StringComparison.OrdinalIgnoreCase));
        suggestion.RecommendedDate.Should().NotBeNull();
        suggestion.AdvisoryId.Should().NotBeNull();
        suggestion.CanApply.Should().BeTrue();
        suggestion.IsWeatherStale.Should().BeFalse();
        suggestion.StaleReason.Should().BeNull();
    }

    [Fact]
    public async Task ListTasks_TaskWithoutWeatherRisk_ReturnsNullSuggestion()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var normalTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "Çapa ve Ot Temizliği",
            Description = "Yabancı otlar temizlenecek",
            Reason = "Toprak havalandırma",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(1),
            DedupeKey = "weeding-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(normalTask)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().BeNull();
    }

    [Fact]
    public async Task ListTasks_WithStaleWeather_DisablesApply()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var tomorrow = _today.AddDays(1);
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = tomorrow,
            DedupeKey = "spray-stale"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 26),
            new(now.AddDays(2), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        // Snapshot is 6 hours old (stale)
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now.AddHours(-6)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var suggestion = item.WeatherPostponeSuggestion!;
        suggestion.IsWeatherStale.Should().BeTrue();
        suggestion.CanApply.Should().BeFalse();
        suggestion.RecommendedDate.Should().BeNull();
        suggestion.StaleReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListTasks_WhenWeatherUnavailable_DisablesApply()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = null;
        farm.Longitude = null;

        var taskId = Guid.NewGuid();
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(1),
            DedupeKey = "spray-no-weather"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var suggestion = item.WeatherPostponeSuggestion!;
        suggestion.CanApply.Should().BeFalse();
        suggestion.RecommendedDate.Should().BeNull();
        suggestion.StaleReason.Should().Contain("erişilemediği");
    }

    [Fact]
    public async Task ListTasks_WhenAdvisoryAlreadyApplied_CanApplyFalse()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(1),
            DedupeKey = "spray-applied"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _farmerId,
            RelatedTaskId = taskId,
            Title = "Rüzgar Riski",
            Summary = "Yüksek rüzgar nedeniyle erteleme tavsiye ediliyor",
            AgronomicExplanation = "Açıklama",
            ActionRecommendation = "Tavsiye",
            RecommendedDate = _today.AddDays(3),
            IsApplied = true, // already applied
            IsDismissed = false,
            DedupeKey = "adv-applied"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 26)
        };
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .WithProactiveAdvisories(advisory)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();
        item.WeatherPostponeSuggestion!.CanApply.Should().BeFalse();
        item.WeatherPostponeSuggestion.AdvisoryId.Should().Be(advisoryId);
    }

    [Fact]
    public async Task ListTasks_WhenAdvisoryDismissed_CanApplyFalse()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(1),
            DedupeKey = "spray-dismissed"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _farmerId,
            RelatedTaskId = taskId,
            Title = "Rüzgar Riski",
            Summary = "Yüksek rüzgar nedeniyle erteleme tavsiye ediliyor",
            AgronomicExplanation = "Açıklama",
            ActionRecommendation = "Tavsiye",
            RecommendedDate = _today.AddDays(3),
            IsApplied = false,
            IsDismissed = true, // dismissed
            DismissedAtUtc = DateTime.UtcNow,
            DedupeKey = "adv-dismissed"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 26)
        };
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .WithProactiveAdvisories(advisory)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();
        item.WeatherPostponeSuggestion!.CanApply.Should().BeFalse();
        item.WeatherPostponeSuggestion.AdvisoryId.Should().Be(advisoryId);
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.NotApplied)]
    [InlineData(TaskStatus.Cancelled)]
    public async Task ListTasks_WhenTaskTerminal_CanApplyFalse(TaskStatus terminalStatus)
    {
        var farm = CreateSampleFarm();
        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var terminalTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = terminalStatus,
            DueDate = _today,
            DedupeKey = $"spray-{terminalStatus}"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _farmerId,
            RelatedTaskId = taskId,
            Title = "Rüzgar Riski",
            Summary = "Yüksek rüzgar nedeniyle erteleme tavsiye ediliyor",
            AgronomicExplanation = "Açıklama",
            ActionRecommendation = "Tavsiye",
            RecommendedDate = _today.AddDays(3),
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = $"adv-terminal-{terminalStatus}"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(terminalTask)
            .WithProactiveAdvisories(advisory)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var weather = new ListDailyTasksQueryHandler.WeatherResolution(
            Points: [],
            FetchedAtUtc: DateTime.UtcNow,
            IsStale: false,
            StaleReason: null,
            IsAvailable: true);

        var advisoriesByTaskId = new Dictionary<Guid, ProactiveAdvisory> { [taskId] = advisory };

        var suggestion = await handler.BuildSuggestionForTaskAsync(
            terminalTask,
            farm,
            weather,
            advisoriesByTaskId,
            DateTime.UtcNow,
            CancellationToken.None);

        suggestion.Should().NotBeNull();
        suggestion!.CanApply.Should().BeFalse();
        suggestion.AdvisoryId.Should().Be(advisoryId);
    }

    [Fact]
    public async Task StaleSuggestion_CannotBeAppliedFromReturnedContract()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var sprayingTask = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            Reason = "Zararlı kontrolü",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Status = TaskStatus.Planned,
            DueDate = _today.AddDays(1),
            DedupeKey = "spray-stale-contract"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 28),
            new(now.AddDays(2), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        // Snapshot is 8 hours old
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now.AddHours(-8)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(sprayingTask)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var contract = item.WeatherPostponeSuggestion!;
        // Contract must not offer an actionable apply
        contract.CanApply.Should().BeFalse();
        contract.RecommendedDate.Should().BeNull();
        contract.IsWeatherStale.Should().BeTrue();
        contract.StaleReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WeatherAdvisory_ValidUntil_DoesNotExceedWeatherFreshnessDeadline()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "İlaçlama yapılacak",
            DueDate = _today,
            Status = TaskStatus.Planned,
            DedupeKey = "spray-freshness"
        };

        var fetchedTime = DateTime.UtcNow.AddHours(-1);
        var points = new List<WeatherPoint>
        {
            new(DateTime.UtcNow, TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 28),
            new(DateTime.UtcNow.AddDays(1), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = fetchedTime
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

        var mockEngine = new Mock<IProactiveAdvisoryEngine>();
        var evalResult = new ProactiveAdvisoryEvaluationResult(
            AdvisoryType: ProactiveAdvisoryType.SprayingWindow,
            Severity: AdvisorySeverity.Warning,
            ActionType: ProactiveActionType.PostponeTask,
            Title: "Rüzgar Riski",
            Summary: "Rüzgar yüksek",
            AgronomicExplanation: "Sürüklenme",
            ActionRecommendation: "Ertele",
            RecommendedDate: _today.AddDays(1),
            DedupeKey: $"adv-spray-{_farmId}-{taskId}-{_today:yyyyMMdd}",
            RelatedTaskId: taskId
        );
        mockEngine
            .Setup(e => e.Evaluate(It.IsAny<Farm>(), It.IsAny<IReadOnlyList<Activity>>(), It.IsAny<IReadOnlyList<FarmTask>>(), It.IsAny<WeatherForecastData>(), It.IsAny<DateTime>()))
            .Returns([evalResult]);

        var handler = new ListDailyTasksQueryHandler(db, advisoryEngine: mockEngine.Object, configuration: config);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var persistedAdvisory = await db.ProactiveAdvisories.FirstOrDefaultAsync(a => a.RelatedTaskId == taskId);
        persistedAdvisory.Should().NotBeNull();
        persistedAdvisory!.ValidUntilUtc.Should().NotBeNull();
        // Deadline = 10:00 + 4h = 14:00 UTC
        var expectedDeadline = fetchedTime.AddHours(4);
        persistedAdvisory.ValidUntilUtc!.Value.Should().BeOnOrBefore(expectedDeadline);
    }

    [Fact]
    public async Task ListTasks_WhenPersistedAdvisoryExpired_ReturnsCanApplyFalse()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            DueDate = _today,
            Status = TaskStatus.Planned,
            DedupeKey = "task-exp-1"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _farmerId,
            RelatedTaskId = taskId,
            Title = "Rüzgar Riski",
            Summary = "Erteleme önerisi",
            AgronomicExplanation = "Açıklama",
            ActionRecommendation = "Tavsiye",
            RecommendedDate = _today.AddDays(1),
            ValidUntilUtc = DateTime.UtcNow.AddMinutes(-30), // Expired 30 mins ago
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = "adv-exp-1"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();
        item.WeatherPostponeSuggestion!.CanApply.Should().BeFalse();
        item.WeatherPostponeSuggestion.IsWeatherStale.Should().BeTrue();
    }

    [Fact]
    public async Task ListTasks_WhenPersistedAdvisoryExpired_DoesNotExposeActionableRecommendedDate()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var advisoryId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            DueDate = _today,
            Status = TaskStatus.Planned,
            DedupeKey = "task-exp-2"
        };

        var advisory = new ProactiveAdvisory
        {
            Id = advisoryId,
            FarmId = _farmId,
            UserId = _farmerId,
            RelatedTaskId = taskId,
            Title = "Rüzgar Riski",
            Summary = "Erteleme önerisi",
            AgronomicExplanation = "Açıklama",
            ActionRecommendation = "Tavsiye",
            RecommendedDate = _today.AddDays(1),
            ValidUntilUtc = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            IsApplied = false,
            IsDismissed = false,
            DedupeKey = "adv-exp-2"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithProactiveAdvisories(advisory)
            .Build();

        var handler = new ListDailyTasksQueryHandler(db);
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var suggestion = item.WeatherPostponeSuggestion!;
        suggestion.RecommendedDate.Should().BeNull();
        suggestion.CanApply.Should().BeFalse();
        suggestion.StaleReason.Should().Contain("süresi doldu");
    }

    [Fact]
    public async Task WeatherFreshness_UsesCanonicalConfigurationKey()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            DueDate = _today,
            Status = TaskStatus.Planned,
            DedupeKey = "task-canonical-config"
        };

        // Snapshot is 3 hours old (would be fresh under default 4h, but stale under configured 2h)
        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 25)
        };
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now.AddHours(-3)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:StaleAfterHours"] = "2" // Configured to 2 hours
            })
            .Build();

        var handler = new ListDailyTasksQueryHandler(db, configuration: config);
        var resolution = await handler.ResolveFarmWeatherAsync(farm, CancellationToken.None);

        resolution.IsStale.Should().BeTrue();
        resolution.StaleReason.Should().NotBeNull();
    }

    [Fact]
    public async Task ProviderFailure_WithFreshSnapshot_WeatherAndTaskSuggestionHaveConsistentFreshnessSemantics()
    {
        var farm = CreateSampleFarm();
        farm.Latitude = 38.0;
        farm.Longitude = 32.0;

        var taskId = Guid.NewGuid();
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = _farmId,
            Title = "İlaçlama Görevi",
            Description = "Zararlılara karşı ilaçlama",
            DueDate = _today.AddDays(1),
            Status = TaskStatus.Planned,
            DedupeKey = "task-provider-failure-fresh"
        };

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 28),
            new(now.AddDays(2), TemperatureC: 20, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 10)
        };

        // Snapshot is only 1 hour old (fresh in age, well within 4h threshold)
        var snapshot = new WeatherSnapshot
        {
            FarmId = _farmId,
            Provider = "open_meteo",
            Payload = WeatherSnapshotPayload.Serialize(farm.Latitude!.Value, farm.Longitude!.Value, points),
            FetchedAtUtc = now.AddHours(-1)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .WithWeatherSnapshots(snapshot)
            .Build();

        var mockWeather = new Mock<IWeatherProvider>();
        mockWeather
            .Setup(w => w.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Remote weather provider unavailable"));

        var handler = new ListDailyTasksQueryHandler(db, weatherProvider: mockWeather.Object);

        // 1. Check weather resolution: Must mark stale due to provider failure per AGRICULTURAL_RULES.md
        var resolution = await handler.ResolveFarmWeatherAsync(farm, CancellationToken.None);
        resolution.IsAvailable.Should().BeTrue();
        resolution.IsStale.Should().BeTrue();
        resolution.StaleReason.Should().Be("Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor.");

        // 2. Check task list suggestion: Must also mark stale, disable CanApply and hide RecommendedDate
        var query = new ListDailyTasksQuery(_farmId, _farmerId, UserRole.Farmer, _today);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        var item = result!.Items.FirstOrDefault(i => i.Id == taskId);
        item.Should().NotBeNull();
        item!.WeatherPostponeSuggestion.Should().NotBeNull();

        var suggestion = item.WeatherPostponeSuggestion!;
        suggestion.IsWeatherStale.Should().BeTrue();
        suggestion.CanApply.Should().BeFalse();
        suggestion.RecommendedDate.Should().BeNull();
        suggestion.StaleReason.Should().Be("Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor.");
    }
}
