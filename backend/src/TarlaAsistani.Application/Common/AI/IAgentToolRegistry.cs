namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Registry and safe dispatcher for AI agent tools.
/// Serves as the single source of truth for available tool definitions and safe tool execution.
/// </summary>
public interface IAgentToolRegistry
{
    /// <summary>
    /// Gets all registered tool definitions available to expose to AI models.
    /// </summary>
    IReadOnlyCollection<AIToolDefinition> GetToolDefinitions();

    /// <summary>
    /// Resolves a registered tool by its name.
    /// </summary>
    /// <param name="toolName">The name of the tool to resolve.</param>
    /// <returns>The registered <see cref="IAgentTool"/>, or null if not found.</returns>
    IAgentTool? GetTool(string toolName);

    /// <summary>
    /// Attempts to resolve a registered tool by its name.
    /// </summary>
    /// <param name="toolName">The name of the tool to resolve.</param>
    /// <param name="tool">The registered tool instance, if found.</param>
    /// <returns>True if the tool is registered; otherwise, false.</returns>
    bool TryGetTool(string toolName, out IAgentTool? tool);

    /// <summary>
    /// Safely executes an AI tool call, handling unregistered tools and unexpected exceptions gracefully.
    /// </summary>
    /// <param name="call">The tool call requested by the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result (success or sanitized failure).</returns>
    Task<AIToolResult> ExecuteToolAsync(
        AIToolCall call,
        CancellationToken cancellationToken = default);
}
