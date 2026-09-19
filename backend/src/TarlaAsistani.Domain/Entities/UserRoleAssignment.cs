using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class UserRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? GrantedByUserId { get; set; }
    public string? GrantReason { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public User? GrantedByUser { get; set; }
    public User? RevokedByUser { get; set; }
}
