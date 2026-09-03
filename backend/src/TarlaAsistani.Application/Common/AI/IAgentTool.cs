using System.Text.RegularExpressions;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Represents an executable tool exposed to an AI agent model.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Gets the unique name of the tool. Must match <see cref="Definition"/>.<see cref="AIToolDefinition.Name"/>
    /// and comply with the lowercase snake_case convention (e.g., "create_task", "get_weather").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the definition and parameter schema exposed to the AI model.
    /// </summary>
    AIToolDefinition Definition { get; }

    /// <summary>
    /// Executes the tool with the provided arguments and returns an execution result.
    /// </summary>
    /// <param name="call">The tool call requested by the model containing structured arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<AIToolResult> ExecuteAsync(
        AIToolCall call,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Helper for validating AI tool naming conventions.
/// </summary>
public static class AgentToolNameValidator
{
    private static readonly Regex ToolNameRegex = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Checks whether a given tool name adheres to the standard lowercase snake_case format.
    /// </summary>
    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ToolNameRegex.IsMatch(name);

    /// <summary>
    /// Validates a tool name, throwing an <see cref="ArgumentException"/> if invalid.
    /// </summary>
    public static void Validate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!IsValid(name))
        {
            throw new ArgumentException(
                $"Tool name '{name}' is invalid. Tool names must follow lowercase snake_case format '^[a-z][a-z0-9_]*$' (e.g. 'create_task', 'get_weather').",
                nameof(name));
        }
    }
}
