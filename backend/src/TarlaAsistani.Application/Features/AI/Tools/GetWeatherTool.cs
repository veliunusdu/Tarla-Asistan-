using System.Text.Json.Serialization;
using MediatR;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.Queries;

namespace TarlaAsistani.Application.Features.AI.Tools;

/// <summary>
/// Tool for fetching weather and forecast information for a specific farm.
/// </summary>
public class GetWeatherTool : IAgentTool
{
    private const string ToolDescription =
        "Returns current weather conditions and forecast for a specific farm accessible to the authenticated user. Requires a valid farm_id. If only a farm name is known, call list_farms first to obtain its farm_id.";

    private const string ParameterSchemaJson = """
    {
      "type": "object",
      "properties": {
        "farm_id": {
          "type": "string",
          "format": "uuid",
          "description": "The unique identifier of the farm returned by list_farms."
        }
      },
      "required": ["farm_id"],
      "additionalProperties": false
    }
    """;

    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUserContext;

    /// <inheritdoc />
    public string Name => "get_weather";

    /// <inheritdoc />
    public AIToolDefinition Definition { get; } =
        AIToolDefinition.Create("get_weather", ToolDescription, ParameterSchemaJson);

    public GetWeatherTool(IMediator mediator, ICurrentUserContext currentUserContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    private sealed record GetWeatherArgs(
        [property: JsonPropertyName("farm_id")] string? FarmId
    );

    /// <inheritdoc />
    public async Task<AIToolResult> ExecuteAsync(AIToolCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "User is not authenticated.",
                errorCode: "unauthenticated");
        }

        // Validate arguments
        GetWeatherArgs? args;
        try
        {
            args = call.DeserializeArguments<GetWeatherArgs>();
        }
        catch
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Failed to parse tool arguments.",
                errorCode: "invalid_arguments");
        }

        if (args == null || string.IsNullOrWhiteSpace(args.FarmId))
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Missing required argument 'farm_id'.",
                errorCode: "invalid_arguments");
        }

        if (!Guid.TryParse(args.FarmId, out var farmId) || farmId == Guid.Empty)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The provided 'farm_id' is not a valid UUID.",
                errorCode: "invalid_arguments");
        }

        var query = new GetFarmWeatherQuery(
            FarmId: farmId,
            UserId: _currentUserContext.UserId.Value,
            Role: _currentUserContext.Role);

        try
        {
            var weather = await _mediator.Send(query, cancellationToken);

            var current = weather.Current != null
                ? new WeatherCurrentSummaryDto(
                    TemperatureC: weather.Current.TemperatureC,
                    FeelsLikeC: weather.Current.FeelsLikeC,
                    HumidityPercent: weather.Current.HumidityPercent,
                    WindSpeedKmh: weather.Current.WindSpeedKmh,
                    Condition: weather.Current.Condition)
                : null;

            var daily = weather.Daily?.Select(d => new WeatherDailySummaryDto(
                Date: d.Date,
                MaxTemperatureC: d.MaxTemperatureC,
                MinTemperatureC: d.MinTemperatureC,
                PrecipitationProbability: d.PrecipitationProbability,
                PrecipitationMm: d.PrecipitationMm,
                Condition: d.Condition
            )).ToList() ?? new List<WeatherDailySummaryDto>();

            var riskDescriptions = weather.Risks?.Select(r => $"[{r.Severity}] {r.Message}").ToList() ?? new List<string>();

            var payload = new GetWeatherResultDto(
                FarmId: weather.FarmId,
                Current: current,
                Daily: daily,
                IsStale: weather.IsStale,
                StaleReason: weather.StaleReason,
                Risks: riskDescriptions);

            return AIToolResult.Success(call.CallId, Name, payload, AIToolJsonOptions.Default);
        }
        catch (KeyNotFoundException)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Farm was not found or is not accessible to the current user.",
                errorCode: "farm_not_found");
        }
        catch (ArgumentException ex)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                ex.Message,
                errorCode: "weather_unavailable");
        }
        catch (InvalidOperationException ex)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                ex.Message,
                errorCode: "weather_unavailable");
        }
    }
}

public sealed record WeatherCurrentSummaryDto(
    double? TemperatureC,
    double? FeelsLikeC,
    double? HumidityPercent,
    double? WindSpeedKmh,
    string? Condition
);

public sealed record WeatherDailySummaryDto(
    DateOnly Date,
    double? MaxTemperatureC,
    double? MinTemperatureC,
    double? PrecipitationProbability,
    double? PrecipitationMm,
    string? Condition
);

public sealed record GetWeatherResultDto(
    Guid FarmId,
    WeatherCurrentSummaryDto? Current,
    IReadOnlyList<WeatherDailySummaryDto> Daily,
    bool IsStale,
    string? StaleReason,
    IReadOnlyList<string> Risks
);
