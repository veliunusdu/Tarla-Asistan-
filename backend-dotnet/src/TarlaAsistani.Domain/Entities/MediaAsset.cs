using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }

    public MediaKind Kind { get; set; }

    /// <summary>Original file name as uploaded. Max 255 chars.</summary>
    public string OriginalName { get; set; } = null!;

    /// <summary>MIME type (e.g. "image/jpeg"). Max 100 chars.</summary>
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    /// <summary>Unique storage path / object key. Max 255 chars.</summary>
    public string StorageKey { get; set; } = null!;

    /// <summary>Hex-encoded SHA-256 checksum. Max 64 chars.</summary>
    public string ChecksumSha256 { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Computed property — not persisted
    public string Url => $"/api/v1/media/{Id}/content";

    // Navigation
    public User Owner { get; set; } = null!;
}
