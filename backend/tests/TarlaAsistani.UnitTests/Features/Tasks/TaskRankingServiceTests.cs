using FluentAssertions;
using TarlaAsistani.Application.Features.Tasks.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.Tasks;

[Trait("Category", "Tasks")]
public class TaskRankingServiceTests
{
    private readonly DateOnly _targetDate = new(2026, 9, 5);

    [Fact]
    public void Test1_PriorityHierarchy_Critical_Beats_High_Beats_Medium_Beats_Low()
    {
        // 1. Critical > High > Medium > Low temel sıralama.
        var farmId = Guid.NewGuid();
        var lowTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Low Task",
            Priority = TaskPriority.Low,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };
        var mediumTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Medium Task",
            Priority = TaskPriority.Medium,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };
        var highTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "High Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };
        var criticalTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Critical Task",
            Priority = TaskPriority.Critical,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };

        var ranked = TaskRankingService.RankTasks(
            new[] { lowTask, criticalTask, mediumTask, highTask },
            _targetDate);

        ranked.Select(t => t.Priority).Should().ContainInOrder(
            TaskPriority.Critical,
            TaskPriority.High,
            TaskPriority.Medium,
            TaskPriority.Low);
    }

    [Fact]
    public void Test2_SamePriority_MoreUrgentDueDateComesFirst()
    {
        // 2. Aynı priority'de daha acil dueDate önce.
        var farmId = Guid.NewGuid();
        var dueToday = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Due Today",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };
        var dueTomorrow = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Due Tomorrow",
            Priority = TaskPriority.High,
            DueDate = _targetDate.AddDays(1),
            Source = TaskSource.CropCalendar
        };
        var dueIn3Days = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Due In 3 Days",
            Priority = TaskPriority.High,
            DueDate = _targetDate.AddDays(3),
            Source = TaskSource.CropCalendar
        };
        var dueIn7Days = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Due In 7 Days",
            Priority = TaskPriority.High,
            DueDate = _targetDate.AddDays(7),
            Source = TaskSource.CropCalendar
        };

        var ranked = TaskRankingService.RankTasks(
            new[] { dueIn7Days, dueTomorrow, dueIn3Days, dueToday },
            _targetDate);

        ranked.Select(t => t.Title).Should().ContainInOrder(
            "Due Today",
            "Due Tomorrow",
            "Due In 3 Days",
            "Due In 7 Days");
    }

    [Fact]
    public void Test3_ExpertSourceCannotOvertakeHigherPriorityTask()
    {
        // 3. Expert source tek başına higher priority task'ı geçemez.
        var farmId = Guid.NewGuid();
        var lowExpert = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Low Expert",
            Priority = TaskPriority.Low,
            Source = TaskSource.Expert,
            DueDate = _targetDate
        };
        var highCropCalendar = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "High CropCalendar",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            DueDate = _targetDate
        };
        var mediumSystem = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Medium System",
            Priority = TaskPriority.Medium,
            Source = TaskSource.System,
            DueDate = _targetDate
        };

        var ranked = TaskRankingService.RankTasks(
            new[] { lowExpert, highCropCalendar, mediumSystem },
            _targetDate);

        ranked.First().Title.Should().Be("High CropCalendar");
        ranked[1].Title.Should().Be("Medium System");
        ranked[2].Title.Should().Be("Low Expert");
    }

    [Fact]
    public void Test4_SamePriorityAndUrgency_SourceTieBreakIsDeterministic()
    {
        // 4. Aynı priority/aciliyet durumunda source tie-break deterministik:
        // Expert > Weather > CropCalendar > Manual > System
        var farmId = Guid.NewGuid();
        var expert = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Expert Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.Expert
        };
        var weather = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Weather Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.Weather
        };
        var cropCalendar = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "CropCalendar Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };
        var manual = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Manual Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.Manual
        };
        var system = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "System Task",
            Priority = TaskPriority.High,
            DueDate = _targetDate,
            Source = TaskSource.System
        };

        var ranked = TaskRankingService.RankTasks(
            new[] { system, manual, cropCalendar, weather, expert },
            _targetDate);

        ranked.Select(t => t.Source).Should().ContainInOrder(
            TaskSource.Expert,
            TaskSource.Weather,
            TaskSource.CropCalendar,
            TaskSource.Manual,
            TaskSource.System);
    }

    [Fact]
    public void Test5_ConfidenceTieBreakWorksCorrectly()
    {
        // 5. Confidence tie-break doğru çalışır:
        // High > Medium > Low (aynı priority, aciliyet ve source durumunda)
        var farmId = Guid.NewGuid();
        var lowConf = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Low Confidence",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.Low,
            DueDate = _targetDate
        };
        var medConf = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Medium Confidence",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.Medium,
            DueDate = _targetDate
        };
        var highConf = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "High Confidence",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.High,
            DueDate = _targetDate
        };

        var ranked = TaskRankingService.RankTasks(
            new[] { lowConf, highConf, medConf },
            _targetDate);

        ranked.Select(t => t.Confidence).Should().ContainInOrder(
            TaskConfidence.High,
            TaskConfidence.Medium,
            TaskConfidence.Low);
    }

    [Fact]
    public void Test6_UnknownPriorityDoesNotCrash()
    {
        // 6. Unknown priority crash oluşturmaz.
        var farmId = Guid.NewGuid();
        var unknownTask = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Unknown Priority Task",
            Priority = (TaskPriority)999,
            DueDate = _targetDate,
            Source = TaskSource.CropCalendar
        };

        var act = () => TaskRankingService.RankTasks(new[] { unknownTask }, _targetDate);
        act.Should().NotThrow();

        var score = TaskRankingService.CalculateScore(unknownTask, _targetDate);
        score.Should().BeGreaterThan(0); // urgency + source + conf adjustments still work
    }

    [Fact]
    public void Test7_NullDueDateDoesNotCrash()
    {
        // 7. Null dueDate crash oluşturmaz.
        var urgency = TaskRankingService.CalculateUrgencyScore(null, _targetDate);
        urgency.Should().Be(0);
    }

    [Fact]
    public void Test8_DeterministicOrder_SameInputReturnsSameOrder()
    {
        // 8. Aynı input iki kez çalıştırıldığında sıra aynıdır.
        var farmId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tasks = Enumerable.Range(1, 10).Select(i => new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = $"Task {i}",
            Priority = TaskPriority.Medium,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.Medium,
            DueDate = _targetDate,
            CreatedAtUtc = now
        }).ToList();

        var run1 = TaskRankingService.RankTasks(tasks, _targetDate).Select(t => t.Id).ToList();
        var run2 = TaskRankingService.RankTasks(tasks, _targetDate).Select(t => t.Id).ToList();

        run1.Should().Equal(run2);
    }

    [Fact]
    public void Test9_SortOccursBeforeTake3()
    {
        // 9. Sort işlemi Take(3) öncesinde gerçekleşir.
        // 5 aday arasından en yüksek puanlı 3 tanesi seçilir.
        var farmId = Guid.NewGuid();
        var low1 = new FarmTask { Id = Guid.NewGuid(), FarmId = farmId, Title = "L1", Priority = TaskPriority.Low, DueDate = _targetDate };
        var low2 = new FarmTask { Id = Guid.NewGuid(), FarmId = farmId, Title = "L2", Priority = TaskPriority.Low, DueDate = _targetDate };
        var low3 = new FarmTask { Id = Guid.NewGuid(), FarmId = farmId, Title = "L3", Priority = TaskPriority.Low, DueDate = _targetDate };
        var high1 = new FarmTask { Id = Guid.NewGuid(), FarmId = farmId, Title = "H1", Priority = TaskPriority.High, DueDate = _targetDate };
        var critical1 = new FarmTask { Id = Guid.NewGuid(), FarmId = farmId, Title = "C1", Priority = TaskPriority.Critical, DueDate = _targetDate };

        var input = new[] { low1, low2, low3, high1, critical1 };
        var top3 = TaskRankingService.RankTasks(input, _targetDate).Take(3).ToList();

        top3.Select(t => t.Title).Should().Equal("C1", "H1", "L1");
    }

    [Fact]
    public void Test10_ScenarioA_FiveCandidates_CorrectThreeSelected()
    {
        // 10. 5 adaydan doğru 3 tanesi seçilir (Senaryo A).
        // Adaylar:
        // 1. High CropCalendar: Sulama
        // 2. High Expert: Yaprakları kontrol et
        // 3. Medium System: Gübreleme
        // 4. Low Expert: Haftalık genel kontrol
        // 5. Low System: Arşiv kontrolü
        var farmId = Guid.NewGuid();
        var highCrop = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Sulama",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            DueDate = _targetDate
        };
        var highExpert = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Yaprakları kontrol et",
            Priority = TaskPriority.High,
            Source = TaskSource.Expert,
            DueDate = _targetDate
        };
        var medSystem = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Gübreleme",
            Priority = TaskPriority.Medium,
            Source = TaskSource.System,
            DueDate = _targetDate
        };
        var lowExpert = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Haftalık genel kontrol",
            Priority = TaskPriority.Low,
            Source = TaskSource.Expert,
            DueDate = _targetDate
        };
        var lowSystem = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "Arşiv kontrolü",
            Priority = TaskPriority.Low,
            Source = TaskSource.System,
            DueDate = _targetDate
        };

        var top3 = TaskRankingService.RankTasks(
            new[] { lowExpert, medSystem, highCrop, lowSystem, highExpert },
            _targetDate).Take(3).ToList();

        top3.Select(t => t.Title).Should().Equal(
            "Yaprakları kontrol et", // High Expert (700+40+15=755)
            "Sulama",                // High CropCalendar (700+40+6=746)
            "Gübreleme"              // Medium System (400+40+2=442)
        );
        top3.Should().NotContain(t => t.Title == "Haftalık genel kontrol");
        top3.Should().NotContain(t => t.Title == "Arşiv kontrolü");
    }

    [Fact]
    public void Test17_ExpertReviewRecommendedPreservedAfterRanking()
    {
        // 17. ExpertReviewRecommended alanı ranking sonrası kaybolmaz.
        var farmId = Guid.NewGuid();
        var taskWithLowConfidence = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Title = "AI Advisory Task",
            Priority = TaskPriority.High,
            Source = TaskSource.CropCalendar,
            Confidence = TaskConfidence.Low,
            DueDate = _targetDate
        };

        var ranked = TaskRankingService.RankTasks(new[] { taskWithLowConfidence }, _targetDate);

        ranked.First().ExpertReviewRecommended.Should().BeTrue();
        ranked.First().Confidence.Should().Be(TaskConfidence.Low);
    }
}
