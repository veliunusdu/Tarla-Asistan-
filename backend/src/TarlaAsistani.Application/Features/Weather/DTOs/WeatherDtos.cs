using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.Services;

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
        var firstPoint = WeatherPointSelection.ClosestTo(dto.Points, DateTime.UtcNow);
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

public static class WeatherDefaults
{
    public const string StaleAfterHoursConfigKey = "Weather:StaleAfterHours";
    public const int DefaultStaleAfterHours = 4;

    public const string ForecastDaysConfigKey = "Weather:ForecastDays";
    public const int DefaultForecastDays = 7;

    public static int GetStaleAfterHours(Microsoft.Extensions.Configuration.IConfiguration? config) =>
        config?.GetValue(StaleAfterHoursConfigKey, DefaultStaleAfterHours) ?? DefaultStaleAfterHours;

    public static int GetForecastDays(Microsoft.Extensions.Configuration.IConfiguration? config)
    {
        var configured = config?.GetValue(ForecastDaysConfigKey, DefaultForecastDays) ?? DefaultForecastDays;
        return Math.Clamp(configured, 1, 7);
    }

    public static DateTime? CalculateWeatherAdvisoryValidUntil(
        DateTime? weatherFetchedAtUtc,
        DateTime? defaultValidUntilUtc,
        int staleAfterHours,
        DateTime nowUtc)
    {
        if (!weatherFetchedAtUtc.HasValue)
        {
            // Fail-safe: without a trusted fetch timestamp, weather cannot be assumed fresh.
            // Expire immediately at nowUtc so it cannot be applied as fresh.
            return nowUtc;
        }

        var freshnessDeadline = weatherFetchedAtUtc.Value.AddHours(staleAfterHours);
        if (!defaultValidUntilUtc.HasValue)
        {
            return freshnessDeadline;
        }

        return defaultValidUntilUtc.Value < freshnessDeadline
            ? defaultValidUntilUtc.Value
            : freshnessDeadline;
    }
}

