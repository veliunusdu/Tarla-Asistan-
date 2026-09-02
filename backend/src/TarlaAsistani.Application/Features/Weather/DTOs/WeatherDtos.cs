using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Weather.DTOs;

public record WeatherRiskDto(
    string RiskType,
    string Severity,
    DateTime StartsAt,
    DateTime EndsAt,
    string Message,
    string SuggestedAction
);

public record CurrentWeatherDto(
    DateTime? ObservedAt,
    double? TemperatureC,
    double? FeelsLikeC,
    double? HumidityPercent,
    double? WindSpeedKmh,
    double? WindGustsKmh,
    string? Condition,
    int? WeatherCode
);

public record DailyForecastDto(
    DateOnly Date,
    double? MinTemperatureC,
    double? MaxTemperatureC,
    double? PrecipitationProbability,
    double? PrecipitationMm,
    string? Condition,
    int? WeatherCode
);

public record FarmWeatherResponseDto(
    Guid FarmId,
    string Provider,
    DateTime FetchedAt,
    bool IsStale,
    string? StaleReason,
    List<WeatherPoint> Points,
    List<WeatherRiskDto> Risks,
    CurrentWeatherDto? Current = null,
    List<DailyForecastDto>? Daily = null
);

public record FarmWeatherContext(
    Guid FarmId,
    DateTime FetchedAtUtc,
    DateTime? ObservedAtUtc,
    double? CurrentTemperatureC,
    double? CurrentFeelsLikeC,
    double? CurrentHumidityPercent,
    double? CurrentWindSpeedKmh,
    string? Condition,
    int? WeatherCode,
    double? NextRainProbability,
    double? Next24HoursPrecipitationMm,
    bool IsStale,
    string? StaleReason,
    List<WeatherRiskDto> ActiveRisks
);

public static class FarmWeatherExtensions
{
    public static FarmWeatherContext ToAiContext(this FarmWeatherResponseDto dto)
    {
        var firstPoint = dto.Points.FirstOrDefault();
        var next24 = dto.Points.Take(24).ToList();
        var maxRainProb = next24.Count > 0 ? next24.Max(p => p.PrecipitationProbability) : null;
        var totalPrecip = next24.Count > 0 ? next24.Sum(p => p.PrecipitationMm ?? 0) : (double?)null;

        return new FarmWeatherContext(
            FarmId: dto.FarmId,
            FetchedAtUtc: dto.FetchedAt,
            ObservedAtUtc: dto.Current?.ObservedAt ?? firstPoint?.ObservedAt,
            CurrentTemperatureC: dto.Current?.TemperatureC ?? firstPoint?.TemperatureC,
            CurrentFeelsLikeC: dto.Current?.FeelsLikeC ?? firstPoint?.TemperatureC,
            CurrentHumidityPercent: dto.Current?.HumidityPercent ?? firstPoint?.HumidityPercent,
            CurrentWindSpeedKmh: dto.Current?.WindSpeedKmh ?? firstPoint?.WindSpeedKmh,
            Condition: dto.Current?.Condition,
            WeatherCode: dto.Current?.WeatherCode ?? firstPoint?.WeatherCode,
            NextRainProbability: maxRainProb,
            Next24HoursPrecipitationMm: totalPrecip,
            IsStale: dto.IsStale,
            StaleReason: dto.StaleReason,
            ActiveRisks: dto.Risks
        );
    }
}
