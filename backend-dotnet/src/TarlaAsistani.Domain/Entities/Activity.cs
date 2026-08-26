using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public sealed class Activity
{
    // -- Primary key ----------------------------------------------------------
    public Guid Id { get; set; } = Guid.NewGuid();

    // -- Foreign keys ---------------------------------------------------------
    public Guid FarmId { get; set; }
    public Guid? CropPeriodId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? CreatedById { get; set; }

    // -- Enum fields ----------------------------------------------------------
    public ActivityType ActivityType { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.Confirmed;
    public ActivitySource Source { get; set; } = ActivitySource.Manual;

    // -- Core data ------------------------------------------------------------
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public int? DurationMinutes { get; set; }
    public float? Amount { get; set; }
    public string? Unit { get; set; }               // max 40

    // -- Media ----------------------------------------------------------------
    public string? PhotoUrl { get; set; }           // max 2048
    public string? VoiceUrl { get; set; }           // max 2048
    public string? VoiceTranscript { get; set; }

    // -- Extra metadata -------------------------------------------------------
    public string? PerformedBy { get; set; }        // max 120
    public float? Cost { get; set; }

    // -- Timestamps -----------------------------------------------------------
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // -- Idempotency ----------------------------------------------------------
    /// <summary>
    /// Client-supplied idempotency key (optional). Prevents duplicate writes
    /// from offline / voice-first clients that may retry the same operation.
    /// </summary>
    public Guid? ClientOperationId { get; set; }

    // -- Navigation properties ------------------------------------------------
    public Farm Farm { get; set; } = null!;
    public CropPeriod? CropPeriod { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<ActivityRevision> Revisions { get; set; } = new List<ActivityRevision>();

    // -- Domain methods -------------------------------------------------------

    /// <summary>
    /// Soft-archives the activity. Idempotent: does nothing if already archived.
    /// </summary>
    public void Archive()
    {
        if (ArchivedAtUtc is not null)
            return;

        var now = DateTime.UtcNow;
        ArchivedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Restores a previously archived activity.
    /// </summary>
    public void Restore()
    {
        ArchivedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
