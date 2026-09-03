using Microsoft.Extensions.Logging;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Default implementation of <see cref="IAgentToolRegistry"/>.
/// Dispatches tool calls safely, catches runtime exceptions, and enforces tool registration invariants.
/// </summary>
public class AgentToolRegistry : IAgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;
    private readonly List<AIToolDefinition> _definitions;
    private readonly ILogger<AgentToolRegistry>? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AgentToolRegistry"/> with injected tools.
    /// Validates tool naming and detects duplicate tool names at construction time.
    /// </summary>
    /// <param name="tools">The collection of tools registered in dependency injection.</param>
    /// <param name="logger">Optional logger for tool execution events.</param>
    public AgentToolRegistry(
        IEnumerable<IAgentTool>? tools = null,
        ILogger<AgentToolRegistry>? logger = null)
    {
        _logger = logger;
        _tools = new Dictionary<string, IAgentTool>(StringComparer.Ordinal);
        _definitions = new List<AIToolDefinition>();

        if (tools != null)
        {
            foreach (var tool in tools)
            {
                ArgumentNullException.ThrowIfNull(tool, nameof(tools));

                AgentToolNameValidator.Validate(tool.Name);

                if (!string.Equals(tool.Name, tool.Definition.Name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Tool '{tool.GetType().Name}' has mismatched names: Name property is '{tool.Name}' but Definition.Name is '{tool.Definition.Name}'.");
                }

                if (!_tools.TryAdd(tool.Name, tool))
                {
                    throw new InvalidOperationException(
                        $"Duplicate AI tool name registered: '{tool.Name}'. Tool names must be unique across all registered tools.");
                }

                _definitions.Add(tool.Definition);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AIToolDefinition> GetToolDefinitions() =>
        _definitions.AsReadOnly();

    /// <inheritdoc />
    public IAgentTool? GetTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        return _tools.GetValueOrDefault(toolName);
    }

    /// <inheritdoc />
    public bool TryGetTool(string toolName, out IAgentTool? tool)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            tool = null;
            return false;
        }

        return _tools.TryGetValue(toolName, out tool);
    }

    /// <inheritdoc />
    public async Task<AIToolResult> ExecuteToolAsync(
        AIToolCall call,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (!_tools.TryGetValue(call.ToolName, out var tool))
        {
            _logger?.LogWarning(
                "AI requested execution of unregistered tool '{ToolName}' (CallId: {CallId}).",
                call.ToolName,
                call.CallId);

            return AIToolResult.Failure(
                call.CallId,
                call.ToolName,
                $"Tool '{call.ToolName}' is not registered.",
                errorCode: "unknown_tool");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await tool.ExecuteAsync(call, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Propagate cancellation when requested by the caller
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Unexpected error executing AI tool '{ToolName}' (CallId: {CallId}).",
                call.ToolName,
                call.CallId);

            return AIToolResult.Failure(
                call.CallId,
                call.ToolName,
                $"An error occurred while executing tool '{call.ToolName}'.",
                errorCode: "tool_execution_error");
        }
    }
}
