using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class WeatherSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FarmId { get; set; }

    /// <summary>
    /// Name of the weather data provider (e.g. "openweathermap"). Max 50 chars.
    /// </summary>
    public string Provider { get; set; } = null!;

    /// <summary>
    /// Raw provider response stored as a JSON string.
    /// EF mapping to a JSON column is configured via Fluent API in DbContext.
    /// </summary>
    public string Payload { get; set; } = "{}";

    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Farm Farm { get; set; } = null!;
}
