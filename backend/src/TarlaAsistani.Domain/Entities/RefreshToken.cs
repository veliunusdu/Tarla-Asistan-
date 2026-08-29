using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>
    /// Groups tokens that belong to the same rotation family for reuse-detection.
    /// </summary>
    public Guid FamilyId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 hash of the raw token value. Max 64 chars (hex digest).
    /// </summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
