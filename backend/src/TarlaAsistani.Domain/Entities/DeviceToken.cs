using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>FCM / APNS push token. Max 512 chars, unique.</summary>
    public string Token { get; set; } = null!;

    public DevicePlatform Platform { get; set; }

    public bool Active { get; set; } = true;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
