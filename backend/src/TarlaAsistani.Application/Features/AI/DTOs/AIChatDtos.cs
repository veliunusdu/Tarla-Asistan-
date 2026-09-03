using System.Text.Json.Serialization;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Application.Features.AI.DTOs;

public record ChatHistoryItem(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public record AIChatRequestDto(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("field_id")] string? FieldId = null,
    [property: JsonPropertyName("conversation_id")] string? ConversationId = null,
    [property: JsonPropertyName("history")] List<ChatHistoryItem>? History = null,
    [property: JsonIgnore] byte[]? PhotoBytes = null,
    [property: JsonIgnore] string? PhotoContentType = null,
    [property: JsonIgnore] AIAccountContext? AccountContext = null
);

public record AIChatResponseDto(
    [property: JsonPropertyName("reply")] string Reply,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("prompt_tokens")] int? PromptTokens = null,
    [property: JsonPropertyName("completion_tokens")] int? CompletionTokens = null,
    [property: JsonPropertyName("total_tokens")] int? TotalTokens = null,
    [property: JsonPropertyName("estimated_cost_usd")] decimal? EstimatedCostUsd = null,
    [property: JsonPropertyName("quota_info")] AIQuotaStatusDto? QuotaInfo = null
);

public record AIChatStreamChunkDto(
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("done")] bool Done = false,
    [property: JsonPropertyName("conversation_id")] string? ConversationId = null,
    [property: JsonPropertyName("prompt_tokens")] int? PromptTokens = null,
    [property: JsonPropertyName("completion_tokens")] int? CompletionTokens = null,
    [property: JsonPropertyName("total_tokens")] int? TotalTokens = null,
    [property: JsonPropertyName("estimated_cost_usd")] decimal? EstimatedCostUsd = null,
    [property: JsonPropertyName("quota_info")] AIQuotaStatusDto? QuotaInfo = null
);

// ── AI Context DTOs ───────────────────────────────────────────────────────────

/// <summary>
/// Compact weather context for a single farm, safe to embed in AI system prompt.
/// </summary>
public record AIWeatherAiContext(
    string FarmName,
    double? CurrentTemperatureC,
    double? HumidityPercent,
    double? WindSpeedKmh,
    string? Condition,
    double? NextRainProbabilityPct,
    double? Next24HoursPrecipitationMm,
    bool IsStale,
    string? StaleReason,
    DateTime DataTime,
    List<string> RiskSummaries
);

/// <summary>
/// Per-farm summary for AI context. Compact: only AI-relevant fields.
/// </summary>
public record AIFarmSummary(
    Guid FarmId,
    string Name,
    string? CurrentCrop,
    double? AreaHa,
    string? NextTask,
    DateOnly? NextTaskDueDate,
    string? LastActivity,
    DateTime? LastActivityAt,
    /// <summary>null = weather not fetched (no weather intent or no coordinates).</summary>
    AIWeatherAiContext? Weather,
    bool WeatherRequested = false,
    FarmWorkWeatherSignal? WorkWeatherSignal = null
);

/// <summary>
/// Full account context injected into AI system prompt per request.
/// Built from authenticated user's data; never from client-supplied JSON.
/// </summary>
public record AIAccountContext(
    string? DisplayName,
    List<AIFarmSummary> Farms,
    List<ProactiveAdvisoryDto>? Advisories = null
);
