using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class CaseAssignmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CaseId { get; set; }

    public Guid? PreviousExpertId { get; set; }

    public Guid? NewExpertId { get; set; }

    public Guid ChangedByUserId { get; set; }

    public CaseAssignmentChangeType ChangeType { get; set; }

    public string? Reason { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public SupportCase Case { get; set; } = null!;
    public User? PreviousExpert { get; set; }
    public User? NewExpert { get; set; }
    public User ChangedByUser { get; set; } = null!;
}
