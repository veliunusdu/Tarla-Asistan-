namespace TarlaAsistani.Application.Common.Interfaces;

public record WeatherPoint(
    DateTime ObservedAt,
    double? TemperatureC,
    double? PrecipitationProbability,
    double? PrecipitationMm,
    double? WindSpeedKmh
);

public interface IWeatherProvider
{
    string Name { get; }
    Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
