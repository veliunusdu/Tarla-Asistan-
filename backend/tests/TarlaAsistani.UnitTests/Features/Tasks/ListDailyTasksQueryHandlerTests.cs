using FluentAssertions;
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
}
