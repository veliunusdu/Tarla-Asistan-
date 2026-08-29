using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class SupportCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FarmId { get; set; }

    public Guid CreatedById { get; set; }

    public Guid? AssignedExpertId { get; set; }

    public CaseCategory Category { get; set; }

    public CasePriority Priority { get; set; } = CasePriority.Medium;

    public CaseStatus Status { get; set; } = CaseStatus.Open;

    /// <summary>Max 160 chars.</summary>
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Farm Farm { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public User? AssignedExpert { get; set; }

    public ICollection<CaseMessage> Messages { get; set; } = new List<CaseMessage>();

    public ICollection<CaseMedia> MediaLinks { get; set; } = new List<CaseMedia>();
}
