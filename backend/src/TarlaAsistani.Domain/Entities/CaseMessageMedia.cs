using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

/// <summary>
/// Join table linking <see cref="CaseMessage"/> to <see cref="MediaAsset"/>.
/// Composite PK: (MessageId, MediaId) — configured via Fluent API.
/// </summary>
public class CaseMessageMedia
{
    public Guid MessageId { get; set; }

    public Guid MediaId { get; set; }

    // Navigation
    public CaseMessage Message { get; set; } = null!;

    public MediaAsset Media { get; set; } = null!;
}
