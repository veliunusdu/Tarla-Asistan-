namespace TarlaAsistani.Domain.Entities;

using TarlaAsistani.Domain.Enums;

public class CropPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }

    public CropType CropType { get; set; }
    public string? Variety { get; set; }

    // DateOnly is the modern C# way to handle dates (no time component)
    public DateOnly PlantedAt { get; set; }
    public DateOnly? HarvestedAt { get; set; }

    public CropPeriodStatus Status { get; set; } = CropPeriodStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Farm Farm { get; set; } = null!;
}
