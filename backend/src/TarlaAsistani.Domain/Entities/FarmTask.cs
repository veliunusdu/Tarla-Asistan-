using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus; // disambiguate from System.Threading.Tasks.TaskStatus

namespace TarlaAsistani.Domain.Entities;

/// <summary>
/// Represents an agronomic task on a farm.
/// Named <c>FarmTask</c> to avoid conflict with <see cref="System.Threading.Tasks.Task"/>.
/// Maps to the Python <c>Task</c> model.
/// </summary>
public class FarmTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FarmId { get; set; }

    public Guid? CropPeriodId { get; set; }

    public Guid? CreatedById { get; set; }

    /// <summary>Max 160 chars.</summary>
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public TaskPriority Priority { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.New;

    public TaskSource Source { get; set; }

    public TaskConfidence Confidence { get; set; } = TaskConfidence.Medium;

    public DateOnly DueDate { get; set; }

    /// <summary>Deduplication key to prevent duplicate AI-generated tasks. Max 64 chars.</summary>
    public string DedupeKey { get; set; } = null!;

    /// <summary>Max 500 chars.</summary>
    public string? NotAppliedReason { get; set; }

    /// <summary>Max 1000 chars.</summary>
    public string? CompletionNote { get; set; }

    /// <summary>Max 2048 chars.</summary>
    public string? PhotoUrl { get; set; }

    public DateTime? ViewedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Computed property — not persisted
    public bool ExpertReviewRecommended => Confidence == TaskConfidence.Low;

    // Navigation
    public Farm Farm { get; set; } = null!;

    public CropPeriod? CropPeriod { get; set; }

    public User? CreatedBy { get; set; }
}
