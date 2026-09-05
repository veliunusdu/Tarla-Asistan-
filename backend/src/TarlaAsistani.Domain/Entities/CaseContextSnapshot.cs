namespace TarlaAsistani.Domain.Entities;

public class CaseContextSnapshot
{
    public Guid CaseId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? SizeInHectares { get; set; }
    public string? IrrigationMethod { get; set; }
    public string? SoilType { get; set; }
    public string? FarmNote { get; set; }
    public string? CropName { get; set; }
    public DateOnly? CropPlantedAt { get; set; }
    public DateOnly? CropHarvestedAt { get; set; }
    public int? CropGrowingDay { get; set; }
    public string? WeatherProvider { get; set; }
    public DateTime? WeatherFetchedAtUtc { get; set; }
    public bool IsBasedOnStaleWeather { get; set; }
    public double? CurrentTemperatureC { get; set; }
    public double? CurrentHumidityPercent { get; set; }
    public double? Next24HoursPrecipitationMm { get; set; }
    public string RecentActivitiesJson { get; set; } = "[]";

    public SupportCase Case { get; set; } = null!;
}
