using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

/// <summary>
/// Join table linking <see cref="SupportCase"/> to <see cref="MediaAsset"/>.
/// Composite PK: (CaseId, MediaId) — configured via Fluent API.
/// </summary>
public class CaseMedia
{
    public Guid CaseId { get; set; }

    public Guid MediaId { get; set; }

    // Navigation
    public SupportCase Case { get; set; } = null!;

    public MediaAsset Media { get; set; } = null!;
}
