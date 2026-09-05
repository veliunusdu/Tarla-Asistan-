using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Services;

public static class TaskRankingService
{
    /// <summary>
    /// Computes a deterministic integer score for a task relative to the target date.
    /// Score components:
    /// - PriorityScore: Critical (1000), High (700), Medium (400), Low (100), Unknown (0)
    /// - UrgencyScore: Past/Today (40), Tomorrow (20), Within 3 days (10), Later (0), Null (0)
    /// - SourceAdjustment: Expert (15), Weather (10), CropCalendar (6), Manual (4), System (2), Unknown (0)
    /// - ConfidenceAdjustment: High (4), Medium (2), Low (0), Unknown (0)
    /// </summary>
    public static int CalculateScore(FarmTask task, DateOnly targetDate)
    {
        var priorityScore = task.Priority switch
        {
            TaskPriority.Critical => 1000,
            TaskPriority.High => 700,
            TaskPriority.Medium => 400,
            TaskPriority.Low => 100,
            _ => 0
        };

        var urgencyScore = CalculateUrgencyScore(task.DueDate, targetDate);

        var sourceAdjustment = task.Source switch
        {
            TaskSource.Expert => 15,
            TaskSource.Weather => 10,
            TaskSource.CropCalendar => 6,
            TaskSource.Manual => 4,
            TaskSource.System => 2,
            _ => 0
        };

        var confidenceAdjustment = task.Confidence switch
        {
            TaskConfidence.High => 4,
            TaskConfidence.Medium => 2,
            TaskConfidence.Low => 0,
            _ => 0
        };

        return priorityScore + urgencyScore + sourceAdjustment + confidenceAdjustment;
    }

    /// <summary>
    /// Calculates the urgency score for a due date safely (handles nulls and future dates).
    /// </summary>
    public static int CalculateUrgencyScore(DateOnly? dueDate, DateOnly targetDate)
    {
        if (!dueDate.HasValue)
        {
            return 0;
        }

        var daysDiff = dueDate.Value.DayNumber - targetDate.DayNumber;
        return daysDiff switch
        {
            < 0 => 40,   // past / overdue
            0 => 40,     // due today
            1 => 20,     // due tomorrow
            <= 3 => 10,  // due in 2-3 days
            _ => 0       // due later
        };
    }

    /// <summary>
    /// Ranks tasks in a fully deterministic order using score and tie-breakers:
    /// 1. Score descending
    /// 2. DueDate ascending
    /// 3. Priority descending
    /// 4. Source priority (Expert > Weather > CropCalendar > Manual > System)
    /// 5. Confidence descending (High > Medium > Low)
    /// 6. CreatedAtUtc ascending (FIFO)
    /// 7. Id ascending (Guid tie-breaker)
    /// </summary>
    public static List<FarmTask> RankTasks(IEnumerable<FarmTask> tasks, DateOnly targetDate)
    {
        return tasks
            .OrderByDescending(t => CalculateScore(t, targetDate))
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => GetSourceTieBreakOrder(t.Source))
            .ThenByDescending(t => t.Confidence)
            .ThenBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .ToList();
    }

    private static int GetSourceTieBreakOrder(TaskSource source) => source switch
    {
        TaskSource.Expert => 1,
        TaskSource.Weather => 2,
        TaskSource.CropCalendar => 3,
        TaskSource.Manual => 4,
        TaskSource.System => 5,
        _ => 99
    };
}
