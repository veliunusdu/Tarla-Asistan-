using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

/// <summary>
/// Idempotency record for client-generated operations.
/// Ensures at-most-once semantics for retried requests.
/// </summary>
public class ClientOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ActorId { get; set; }

    /// <summary>
    /// Client-supplied idempotency key (UUID from the calling device/service).
    /// </summary>
    public Guid ClientOperationId { get; set; }

    /// <summary>
    /// Logical scope / resource collection (e.g. "farms.tasks"). Max 80 chars.
    /// </summary>
    public string Scope { get; set; } = null!;

    /// <summary>
    /// Hash of the request payload to detect replays with different bodies. Max 64 chars.
    /// </summary>
    public string PayloadHash { get; set; } = null!;

    /// <summary>
    /// Type name of the created/affected resource (e.g. "FarmTask"). Max 50 chars.
    /// </summary>
    public string ResourceType { get; set; } = null!;

    public Guid ResourceId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User Actor { get; set; } = null!;
}
