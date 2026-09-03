using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class ProactiveAdvisory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FarmId { get; set; }

    public Guid UserId { get; set; }

    public Guid? CropPeriodId { get; set; }

    public Guid? RelatedTaskId { get; set; }

    public ProactiveAdvisoryType AdvisoryType { get; set; }

    public AdvisorySeverity Severity { get; set; }

    public ProactiveActionType ActionType { get; set; }

    /// <summary>Max 160 chars.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Max 500 chars.</summary>
    public string Summary { get; set; } = null!;

    /// <summary>Detailed scientific/agronomic reason.</summary>
    public string AgronomicExplanation { get; set; } = null!;

    /// <summary>Concrete action description for farmer.</summary>
    public string ActionRecommendation { get; set; } = null!;

    public DateOnly? RecommendedDate { get; set; }

    /// <summary>Serialized JSON metrics, e.g. {"rain_mm": 18.2, "wind_kmh": 24}.</summary>
    public string? MetricsJson { get; set; }

    /// <summary>Deduplication key to prevent duplicate advisories. Max 160 chars, unique.</summary>
    public string DedupeKey { get; set; } = null!;

    public DateTime? ValidUntilUtc { get; set; }

    public bool IsDismissed { get; set; } = false;

    public DateTime? DismissedAtUtc { get; set; }

    public bool IsApplied { get; set; } = false;

    public DateTime? AppliedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Farm Farm { get; set; } = null!;

    public User User { get; set; } = null!;

    public CropPeriod? CropPeriod { get; set; }

    public FarmTask? RelatedTask { get; set; }
}
