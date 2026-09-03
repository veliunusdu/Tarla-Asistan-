using MediatR;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Farms.Queries;

namespace TarlaAsistani.Application.Features.AI.Tools;

/// <summary>
/// Tool for listing farms accessible to the authenticated user.
/// </summary>
public class ListFarmsTool : IAgentTool
{
    private const string ToolDescription =
        "Lists farms accessible to the authenticated user. Use this tool when you need to resolve a farm name to its farm ID before calling another farm-specific tool.";

    private const string ParameterSchemaJson = """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """;

    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUserContext;

    /// <inheritdoc />
    public string Name => "list_farms";

    /// <inheritdoc />
    public AIToolDefinition Definition { get; } =
        AIToolDefinition.Create("list_farms", ToolDescription, ParameterSchemaJson);

    public ListFarmsTool(IMediator mediator, ICurrentUserContext currentUserContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

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

        // Execute existing query via MediatR with trusted user context
        var query = new GetFarmsQuery(
            UserId: _currentUserContext.UserId.Value,
            Role: _currentUserContext.Role,
            IncludeArchived: false);

        var farms = await _mediator.Send(query, cancellationToken);

        var farmSummaries = farms.Select(f => new ListFarmsItemDto(
            Id: f.Id,
            Name: f.Name,
            Crop: f.CurrentCropPeriod?.CropType.ToString(),
            Variety: f.CurrentCropPeriod?.Variety,
            SizeInHectares: f.SizeInHectares,
            HasLocation: f.Latitude.HasValue && f.Longitude.HasValue
        )).ToList();

        var payload = new ListFarmsResultDto(farmSummaries, farmSummaries.Count);
        return AIToolResult.Success(call.CallId, Name, payload, AIToolJsonOptions.Default);
    }
}

public sealed record ListFarmsItemDto(
    Guid Id,
    string Name,
    string? Crop,
    string? Variety,
    double? SizeInHectares,
    bool HasLocation
);

public sealed record ListFarmsResultDto(
    IReadOnlyList<ListFarmsItemDto> Farms,
    int Count
);
