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

public record FarmWeatherResponseDto(
    Guid FarmId,
    string Provider,
    DateTime FetchedAt,
    bool IsStale,
    string? StaleReason,
    List<WeatherPoint> Points,
    List<WeatherRiskDto> Risks
);
