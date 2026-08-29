using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class AccountDeletionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique: one active deletion job per user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Snapshot of the Firebase UID at the time the job was created.
    /// </summary>
    public string? FirebaseUidSnapshot { get; set; }

    public AccountDeletionStatus Status { get; set; } = AccountDeletionStatus.Pending;

    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Short error code from the last failed attempt. Max 80 chars.
    /// </summary>
    public string? LastErrorCode { get; set; }

    public DateTime? NextRetryAtUtc { get; set; }

    public DateTime? ProcessingStartedAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency lease expiry for distributed workers.
    /// </summary>
    public DateTime? LeaseUntilUtc { get; set; }

    /// <summary>
    /// Unique token held by the worker currently processing this job. Max 64 chars.
    /// </summary>
    public string? ProcessingOwnerToken { get; set; }

    // Step-level audit timestamps
    public DateTime? FirebaseTokensRevokedAtUtc { get; set; }

    public DateTime? FirestoreAnonymizedAtUtc { get; set; }

    public DateTime? MediaDeletedAtUtc { get; set; }

    public DateTime? FirebaseAuthDeletedAtUtc { get; set; }

    public DateTime? PostgresAnonymizedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
