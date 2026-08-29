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
    public DateTime? UpdatedAtUtc { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<CropPeriod> CropPeriods { get; set; } = new List<CropPeriod>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<FarmTask> Tasks { get; set; } = new List<FarmTask>();
    public ICollection<WeatherSnapshot> WeatherSnapshots { get; set; } = new List<WeatherSnapshot>();

    public void Archive()
    {
        // Only set the date if it isn't already archived
        if (ArchivedAt is null)
        {
            ArchivedAt = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}