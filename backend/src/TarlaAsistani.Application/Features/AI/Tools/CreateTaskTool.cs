using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.AI.Tools;

/// <summary>
/// Write-capable tool that persists a new farm task for the authenticated user.
/// Reuses existing MediatR command handling and deduplication logic.
/// </summary>
public class CreateTaskTool : IAgentTool
{
    private const string ToolDescription =
        "Creates a persistent farm task for the authenticated user. Use this tool only when the user explicitly asks to create, add, or schedule a task. Do not call it for hypothetical suggestions, recommendations, previews, or questions. If only a farm name is known, call list_farms first.";

    private const string ParameterSchemaJson = """
    {
      "type": "object",
      "properties": {
        "farm_id": {
          "type": "string",
          "format": "uuid",
          "description": "The farm ID returned by list_farms."
        },
        "title": {
          "type": "string",
          "minLength": 2,
          "maxLength": 160,
          "description": "Short task title."
        },
        "due_date": {
          "type": "string",
          "format": "date",
          "description": "Task due date in ISO format YYYY-MM-DD."
        },
        "description": {
          "type": "string",
          "maxLength": 4000,
          "description": "Optional task description."
        },
        "reason": {
          "type": "string",
          "maxLength": 2000,
          "description": "Optional reason explicitly provided by the user."
        },
        "priority": {
          "type": "string",
          "enum": ["low", "medium", "high", "critical"],
          "description": "Optional priority. Defaults to medium."
        }
      },
      "required": [
        "farm_id",
        "title",
        "due_date"
      ],
      "additionalProperties": false
    }
    """;

    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "farm_id",
        "title",
        "due_date",
        "description",
        "reason",
        "priority"
    };

    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUserContext;

    /// <inheritdoc />
    public string Name => "create_task";

    /// <inheritdoc />
    public AIToolDefinition Definition { get; } =
        AIToolDefinition.Create("create_task", ToolDescription, ParameterSchemaJson);

    public CreateTaskTool(IMediator mediator, ICurrentUserContext currentUserContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    private sealed record CreateTaskArgs(
        [property: JsonPropertyName("farm_id")] string? FarmId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("due_date")] string? DueDate,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("priority")] string? Priority
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

        // Strict validation: Reject unknown properties to prevent parameter injection
        if (call.Arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in call.Arguments.EnumerateObject())
            {
                if (!AllowedProperties.Contains(property.Name))
                {
                    return AIToolResult.Failure(
                        call.CallId,
                        Name,
                        $"Unsupported argument '{property.Name}' provided.",
                        errorCode: "invalid_arguments");
                }
            }
        }

        CreateTaskArgs? args;
        try
        {
            args = call.DeserializeArguments<CreateTaskArgs>(AIToolJsonOptions.Default);
        }
        catch
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Failed to parse tool arguments.",
                errorCode: "invalid_arguments");
        }

        if (args == null)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Tool arguments cannot be empty.",
                errorCode: "invalid_arguments");
        }

        // Validate farm_id
        if (string.IsNullOrWhiteSpace(args.FarmId))
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

        // Validate title
        if (string.IsNullOrWhiteSpace(args.Title))
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Missing required argument 'title'.",
                errorCode: "invalid_arguments");
        }

        var normalizedTitle = args.Title.Trim();
        if (normalizedTitle.Length < 2 || normalizedTitle.Length > 160)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The task title must be between 2 and 160 characters.",
                errorCode: "invalid_arguments");
        }

        // Validate due_date
        if (string.IsNullOrWhiteSpace(args.DueDate))
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Missing required argument 'due_date'.",
                errorCode: "invalid_arguments");
        }

        if (!DateOnly.TryParseExact(args.DueDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate))
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The provided 'due_date' must be in ISO format 'YYYY-MM-DD' (e.g. '2026-09-04').",
                errorCode: "invalid_arguments");
        }

        // Validate description limits if provided
        var normalizedDescription = !string.IsNullOrWhiteSpace(args.Description)
            ? args.Description.Trim()
            : normalizedTitle;

        if (normalizedDescription.Length > 4000)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The task description must not exceed 4000 characters.",
                errorCode: "invalid_arguments");
        }

        // Validate reason limits if provided
        var normalizedReason = !string.IsNullOrWhiteSpace(args.Reason)
            ? args.Reason.Trim()
            : "Kullanıcı talebi.";

        if (normalizedReason.Length > 2000)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The task reason must not exceed 2000 characters.",
                errorCode: "invalid_arguments");
        }

        // Validate priority if provided
        var priority = TaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(args.Priority))
        {
            if (!Enum.TryParse<TaskPriority>(args.Priority.Trim(), ignoreCase: true, out var parsedPriority))
            {
                return AIToolResult.Failure(
                    call.CallId,
                    Name,
                    "Invalid priority. Allowed values are: 'low', 'medium', 'high', 'critical'.",
                    errorCode: "invalid_arguments");
            }

            priority = parsedPriority;
        }

        // Dispatch existing CreateExpertTaskCommand
        var command = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: _currentUserContext.UserId.Value,
            Title: normalizedTitle,
            Description: normalizedDescription,
            Reason: normalizedReason,
            Priority: priority,
            Confidence: TaskConfidence.High,
            DueDate: dueDate,
            CropPeriodId: null,
            CreatedByRole: _currentUserContext.Role ?? UserRole.Farmer);

        try
        {
            var createdTask = await _mediator.Send(command, cancellationToken);

            var summary = new CreatedTaskSummaryDto(
                Id: createdTask.Id,
                FarmId: createdTask.FarmId,
                Title: createdTask.Title,
                Description: createdTask.Description,
                Priority: createdTask.Priority.ToString().ToLowerInvariant(),
                Status: createdTask.Status.ToString().ToLowerInvariant(),
                DueDate: createdTask.DueDate.ToString("yyyy-MM-dd"));

            var payload = new CreateTaskResultDto(summary, Created: true);
            return AIToolResult.Success(call.CallId, Name, payload, AIToolJsonOptions.Default);
        }
        catch (FarmNotFoundException)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Farm was not found or is not accessible to the current user.",
                errorCode: "farm_not_found");
        }
        catch (UnauthorizedAccessException)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "Farm was not found or is not accessible to the current user.",
                errorCode: "farm_not_found");
        }
        catch (DuplicateTaskException)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "An equivalent task already exists.",
                errorCode: "duplicate_task");
        }
        catch (CropPeriodMismatchException)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                "The crop period does not match the specified farm.",
                errorCode: "farm_not_found");
        }
        catch (FluentValidation.ValidationException ex)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                ex.Message,
                errorCode: "invalid_arguments");
        }
        catch (TarlaAsistani.Domain.Exceptions.ValidationException ex)
        {
            return AIToolResult.Failure(
                call.CallId,
                Name,
                ex.Message,
                errorCode: "invalid_arguments");
        }
    }
}

public sealed record CreatedTaskSummaryDto(
    Guid Id,
    Guid FarmId,
    string Title,
    string Description,
    string Priority,
    string Status,
    string DueDate
);

public sealed record CreateTaskResultDto(
    CreatedTaskSummaryDto Task,
    bool Created
);
