using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Weather.DTOs;

public enum FarmWorkType
{
    Unknown,
    Spraying,
    Irrigation,
    Fertilizing,
    Sowing,
    Harvest,
}

public enum WeatherActionRiskLevel
{
    Low,
    Medium,
    High,
}

public enum WeatherActionSignalCode
{
    SprayingConditions,
    IrrigationTiming,
    FertilizingConditions,
    SowingConditions,
    HarvestConditions,
    WeatherUnavailable,
    ForecastNotAvailable,
}

public enum WeatherSuggestedAction
{
    Proceed,
    ReviewTiming,
    DelayConsidered,
    WeatherUnavailable,
}

public record FarmWorkWeatherSignal(
    Guid FarmId,
    Guid TaskId,
    FarmWorkType WorkType,
    WeatherActionRiskLevel? RiskLevel,
    WeatherActionSignalCode Code,
    List<string> Reasons,
    WeatherSuggestedAction SuggestedAction,
    DateTime EvaluatedAtUtc,
    bool IsBasedOnStaleWeather
);

public record FarmWorkWeatherEvaluationInput(
    Guid FarmId,
    Guid TaskId,
    string TaskTitle,
    string? TaskDescription,
    DateOnly TaskDueDate,
    IReadOnlyList<WeatherPoint>? ForecastPoints,
    bool WeatherAvailable,
    bool IsStaleWeather,
    DateTime EvaluatedAtUtc
);
