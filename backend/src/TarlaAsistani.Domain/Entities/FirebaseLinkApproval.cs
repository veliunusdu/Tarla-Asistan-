using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class FirebaseLinkApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>
    /// The Firebase UID being linked. Max 128 chars.
    /// </summary>
    public string FirebaseUid { get; set; } = null!;

    /// <summary>
    /// Identity of the approver (e.g. admin email or system name). Max 120 chars.
    /// Deprecated in favor of ApprovedByUserId.
    /// </summary>
    public string ApprovedBy { get; set; } = null!;

    /// <summary>
    /// Admin user ID who approved this link.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    public DateTime ApprovedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Set when the approval token has been consumed during the link flow.
    /// </summary>
    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
