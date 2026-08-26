using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class PilotFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CreatedById { get; set; }

    public FeedbackType FeedbackType { get; set; }

    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

    /// <summary>Optional star/score rating (e.g. 1–5).</summary>
    public int? Rating { get; set; }

    public string Comment { get; set; } = null!;

    public Guid? RelatedTaskId { get; set; }

    public Guid? RelatedCaseId { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User CreatedBy { get; set; } = null!;

    public User? ReviewedBy { get; set; }

    public FarmTask? RelatedTask { get; set; }

    public SupportCase? RelatedCase { get; set; }
}
