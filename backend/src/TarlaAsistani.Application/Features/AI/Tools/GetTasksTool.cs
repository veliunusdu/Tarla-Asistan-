using System.Globalization;
using System.Text.Json.Serialization;
using MediatR;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.AI.Tools;

/// <summary>
/// Tool for retrieving tasks for a specific farm, optionally for a specific calendar date.
/// </summary>
public class GetTasksTool : IAgentTool
{
    private const string ToolDescription =
        "Retrieves tasks for a specific farm accessible to the authenticated user, optionally filtered by a specific calendar date (YYYY-MM-DD). Requires a valid farm_id. If only a farm name is known, call list_farms first to obtain its farm_id.";

    private const string ParameterSchemaJson = """
    {
      "type": "object",
      "properties": {
        "farm_id": {
          "type": "string",
          "format": "uuid",
          "description": "The unique identifier of the farm returned by list_farms."
        },
        "date": {
          "type": "string",
          "format": "date",
          "description": "Optional ISO calendar date (YYYY-MM-DD) to retrieve daily tasks for a specific day."
        }
      },
      "required": ["farm_id"],
      "additionalProperties": false
    }
    """;

    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUserContext;

    /// <inheritdoc />
    public string Name => "get_tasks";

    /// <inheritdoc />
    public AIToolDefinition Definition { get; } =
        AIToolDefinition.Create("get_tasks", ToolDescription, ParameterSchemaJson);

    public GetTasksTool(IMediator mediator, ICurrentUserContext currentUserContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    private sealed record GetTasksArgs(
        [property: JsonPropertyName("farm_id")] string? FarmId,
        [property: JsonPropertyName("date")] string? Date
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
        GetTasksArgs? args;
        try
        {
            args = call.DeserializeArguments<GetTasksArgs>();
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

        DateOnly? targetDate = null;
        if (!string.IsNullOrWhiteSpace(args.Date))
        {
            if (!DateOnly.TryParseExact(args.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return AIToolResult.Failure(
                    call.CallId,
                    Name,
                    "The provided 'date' must be in ISO format 'YYYY-MM-DD' (e.g. '2026-09-04').",
                    errorCode: "invalid_arguments");
            }

            targetDate = parsedDate;
        }

        var userId = _currentUserContext.UserId.Value;
        var role = _currentUserContext.Role ?? UserRole.Farmer;

        if (targetDate.HasValue)
        {
            var dailyQuery = new ListDailyTasksQuery(
                FarmId: farmId,
                UserId: userId,
                Role: role,
                TargetDate: targetDate.Value);

            var dailyResult = await _mediator.Send(dailyQuery, cancellationToken);
            if (dailyResult == null)
            {
                return AIToolResult.Failure(
                    call.CallId,
                    Name,
                    "Farm was not found or is not accessible to the current user.",
                    errorCode: "farm_not_found");
            }

            var allDailyTasks = new List<TaskDto>();
            allDailyTasks.AddRange(dailyResult.Items);
            allDailyTasks.AddRange(dailyResult.CriticalWeatherAlerts);
            allDailyTasks.AddRange(dailyResult.Overdue);

            var summaries = allDailyTasks
                .DistinctBy(t => t.Id)
                .Select(t => new TaskItemSummaryDto(
                    Id: t.Id,
                    Title: t.Title,
                    Description: string.IsNullOrWhiteSpace(t.Description) ? null : t.Description,
                    Priority: t.Priority.ToString(),
                    Status: t.Status.ToString(),
                    DueDate: t.DueDate.ToString("yyyy-MM-dd")
                ))
                .ToList();

            var payload = new GetTasksResultDto(
                FarmId: farmId,
                Date: targetDate.Value.ToString("yyyy-MM-dd"),
                Tasks: summaries,
                Count: summaries.Count);

            return AIToolResult.Success(call.CallId, Name, payload, AIToolJsonOptions.Default);
        }
        else
        {
            var farmTasksQuery = new ListFarmTasksQuery(
                FarmId: farmId,
                UserId: userId,
                Role: role);

            var tasks = await _mediator.Send(farmTasksQuery, cancellationToken);
            if (tasks == null)
            {
                return AIToolResult.Failure(
                    call.CallId,
                    Name,
                    "Farm was not found or is not accessible to the current user.",
                    errorCode: "farm_not_found");
            }

            var summaries = tasks
                .Select(t => new TaskItemSummaryDto(
                    Id: t.Id,
                    Title: t.Title,
                    Description: string.IsNullOrWhiteSpace(t.Description) ? null : t.Description,
                    Priority: t.Priority.ToString(),
                    Status: t.Status.ToString(),
                    DueDate: t.DueDate.ToString("yyyy-MM-dd")
                ))
                .ToList();

            var payload = new GetTasksResultDto(
                FarmId: farmId,
                Date: null,
                Tasks: summaries,
                Count: summaries.Count);

            return AIToolResult.Success(call.CallId, Name, payload, AIToolJsonOptions.Default);
        }
    }
}

public sealed record TaskItemSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    string Priority,
    string Status,
    string DueDate
);

public sealed record GetTasksResultDto(
    Guid FarmId,
    string? Date,
    IReadOnlyList<TaskItemSummaryDto> Tasks,
    int Count
);
