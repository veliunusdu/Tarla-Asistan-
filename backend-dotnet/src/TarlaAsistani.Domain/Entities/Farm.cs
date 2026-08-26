namespace TarlaAsistani.Domain.Entities;

using TarlaAsistani.Domain.Enums;

public class Farm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? SizeInHectares { get; set; }
    public IrrigationMethod? IrrigationMethod { get; set; }
    public string? SoilType { get; set; }
    public string? Note { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<CropPeriod> CropPeriods { get; set; } = new List<CropPeriod>();
}