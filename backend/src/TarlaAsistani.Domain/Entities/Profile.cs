using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class Profile
{
    /// <summary>
    /// One-to-one PK/FK to User.
    /// </summary>
    public Guid UserId { get; set; }

    public string? FullName { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public bool TermsAccepted { get; set; } = false;

    public bool NotificationsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
