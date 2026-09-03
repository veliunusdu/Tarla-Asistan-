using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Role types for messages exchanged in an AI agent conversation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AIAgentRole
{
    /// <summary>System instructions or persona prompt.</summary>
    System,

    /// <summary>Message originating from the human user.</summary>
    User,

    /// <summary>Message originating from the assistant (model text and/or tool call requests).</summary>
    Assistant,

    /// <summary>Message containing the result of a tool execution.</summary>
    Tool
}

/// <summary>
/// Represents a provider-independent message in an AI agent conversation loop.
/// Supports user, system, assistant (with optional tool calls), and tool execution results.
/// </summary>
public sealed record AIAgentMessage
{
    /// <summary>
    /// Gets the role of the message author.
    /// </summary>
    public AIAgentRole Role { get; init; }

    /// <summary>
    /// Gets the textual content of the message.
    /// May be null when an assistant message only contains tool calls.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the tool calls requested in this message (applicable when <see cref="Role"/> is <see cref="AIAgentRole.Assistant"/>).
    /// </summary>
    public IReadOnlyList<AIToolCall> ToolCalls { get; init; } = Array.Empty<AIToolCall>();

    /// <summary>
    /// Gets the tool execution result (applicable when <see cref="Role"/> is <see cref="AIAgentRole.Tool"/>).
    /// </summary>
    public AIToolResult? ToolResult { get; init; }

    /// <summary>
    /// Gets optional provider-specific metadata (e.g. opaque thought signature or session state)
    /// preserved across conversation turns.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ProviderMetadata { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentMessage"/>.
    /// </summary>
    public AIAgentMessage(
        AIAgentRole role,
        string? content = null,
        IEnumerable<AIToolCall>? toolCalls = null,
        AIToolResult? toolResult = null,
        IReadOnlyDictionary<string, string>? providerMetadata = null)
    {
        Role = role;
        Content = content;
        ToolCalls = toolCalls?.ToList().AsReadOnly() ?? (IReadOnlyList<AIToolCall>)Array.Empty<AIToolCall>();
        ToolResult = toolResult;
        ProviderMetadata = providerMetadata;
    }

    /// <summary>
    /// Creates a system message containing prompt instructions.
    /// </summary>
    public static AIAgentMessage CreateSystem(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return new AIAgentMessage(AIAgentRole.System, content: content);
    }

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static AIAgentMessage CreateUser(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return new AIAgentMessage(AIAgentRole.User, content: content);
    }

    /// <summary>
    /// Creates an assistant message with optional text content and tool calls.
    /// </summary>
    public static AIAgentMessage CreateAssistant(
        string? content,
        IEnumerable<AIToolCall>? toolCalls = null,
        IReadOnlyDictionary<string, string>? providerMetadata = null)
    {
        var calls = toolCalls?.ToList().AsReadOnly() ?? (IReadOnlyList<AIToolCall>)Array.Empty<AIToolCall>();
        if (string.IsNullOrWhiteSpace(content) && calls.Count == 0)
        {
            throw new ArgumentException("Assistant message must have either text content or at least one tool call.");
        }

        return new AIAgentMessage(AIAgentRole.Assistant, content: content, toolCalls: calls, providerMetadata: providerMetadata);
    }

    /// <summary>
    /// Creates a tool message from an execution result.
    /// </summary>
    public static AIAgentMessage CreateToolResult(AIToolResult toolResult)
    {
        ArgumentNullException.ThrowIfNull(toolResult);
        return new AIAgentMessage(
            AIAgentRole.Tool,
            content: toolResult.GetContentString(),
            toolResult: toolResult);
    }
}
