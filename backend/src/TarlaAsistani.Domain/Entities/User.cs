using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PhoneNumber { get; set; } = string.Empty;
    public string? FirebaseUid { get; set; }
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
    public DateTime? DeletedAtUtc { get; set; }
    public string? AnonymizedSubjectId { get; set; }
    public UserRole Role { get; set; } = UserRole.Farmer;
    public bool IsVerified { get; set; } = false; // kept for OTP auth flow
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Profile? Profile { get; set; }
    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<FirebaseLinkApproval> FirebaseLinkApprovals { get; set; } = new List<FirebaseLinkApproval>();
    public AccountDeletionJob? AccountDeletionJob { get; set; }
}
