using System.Text.Json.Serialization;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.AI.DTOs;

public record ProactiveAdvisoryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("farm_id")] Guid FarmId,
    [property: JsonPropertyName("farm_name")] string FarmName,
    [property: JsonPropertyName("user_id")] Guid UserId,
    [property: JsonPropertyName("related_task_id")] Guid? RelatedTaskId,
    [property: JsonPropertyName("advisory_type")] ProactiveAdvisoryType AdvisoryType,
    [property: JsonPropertyName("severity")] AdvisorySeverity Severity,
    [property: JsonPropertyName("action_type")] ProactiveActionType ActionType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("agronomic_explanation")] string AgronomicExplanation,
    [property: JsonPropertyName("action_recommendation")] string ActionRecommendation,
    [property: JsonPropertyName("recommended_date")] DateOnly? RecommendedDate,
    [property: JsonPropertyName("metrics_json")] string? MetricsJson,
    [property: JsonPropertyName("is_applied")] bool IsApplied,
    [property: JsonPropertyName("is_dismissed")] bool IsDismissed,
    [property: JsonPropertyName("created_at_utc")] DateTime CreatedAtUtc
);

public record ProactiveAdvisoryEvaluationResult(
    ProactiveAdvisoryType AdvisoryType,
    AdvisorySeverity Severity,
    ProactiveActionType ActionType,
    string Title,
    string Summary,
    string AgronomicExplanation,
    string ActionRecommendation,
    DateOnly? RecommendedDate,
    string DedupeKey,
    Guid? RelatedTaskId = null,
    string? MetricsJson = null
);
