using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class ActivityRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ActivityId { get; set; }

    public Guid? ChangedById { get; set; }

    /// <summary>
    /// Snapshot of the fields that were changed, serialised as a JSON string.
    /// </summary>
    public string PreviousValues { get; set; } = null!;

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Activity Activity { get; set; } = null!;

    public User? ChangedBy { get; set; }
}
