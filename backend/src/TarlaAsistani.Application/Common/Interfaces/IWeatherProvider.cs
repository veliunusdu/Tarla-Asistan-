namespace TarlaAsistani.Application.Common.Interfaces;

using TarlaAsistani.Application.Features.Weather.DTOs;

public record WeatherPoint(
    DateTime ObservedAt,
    double? TemperatureC,
    double? PrecipitationProbability,
    double? PrecipitationMm,
    double? WindSpeedKmh,
    double? HumidityPercent = null,
    int? WeatherCode = null
);

public record WeatherForecastData(
    List<WeatherPoint> Points,
    CurrentWeatherDto? Current = null,
    List<DailyForecastDto>? Daily = null
);

public interface IWeatherProvider
{
    string Name { get; }
    Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var points = await ForecastAsync(latitude, longitude, cancellationToken);
        var first = points?.FirstOrDefault();
        var current = first != null
            ? new CurrentWeatherDto(
                ObservedAt: first.ObservedAt,
                TemperatureC: first.TemperatureC,
                FeelsLikeC: first.TemperatureC,
                HumidityPercent: first.HumidityPercent,
                WindSpeedKmh: first.WindSpeedKmh,
                WindGustsKmh: null,
                Condition: null,
                WeatherCode: first.WeatherCode)
            : null;
        return new WeatherForecastData(points ?? new List<WeatherPoint>(), current, null);
    }

    async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WeatherForecastData>(coordinates.Count);
        foreach (var (latitude, longitude) in coordinates)
        {
            results.Add(await GetWeatherAsync(latitude, longitude, cancellationToken));
        }

        return results;
    }
}
