using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public NotificationType NotificationType { get; set; }

    /// <summary>Max 160 chars.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Max 1000 chars.</summary>
    public string Body { get; set; } = null!;

    /// <summary>Deep-link URI for in-app navigation. Max 500 chars.</summary>
    public string DeepLink { get; set; } = null!;

    /// <summary>Arbitrary key-value metadata serialised as a JSON string.</summary>
    public string Data { get; set; } = "{}";

    /// <summary>Deduplication key to prevent duplicate pushes. Max 160 chars, unique.</summary>
    public string DedupeKey { get; set; } = null!;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>Provider-assigned message ID (e.g. FCM message_id). Max 255 chars.</summary>
    public string? ProviderMessageId { get; set; }

    public int AttemptCount { get; set; } = 0;

    /// <summary>Last error description from the push provider. Max 1000 chars.</summary>
    public string? LastError { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
