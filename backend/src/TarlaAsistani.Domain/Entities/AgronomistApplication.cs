using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class AgronomistApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public AgronomistApplicationStatus Status { get; set; } = AgronomistApplicationStatus.Pending;

    public string? ApplicantNote { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
