namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Encapsulates the conversation history, available tools, and optional instructions for an AI agent interaction.
/// </summary>
public sealed record AIAgentRequest
{
    /// <summary>
    /// Gets the ordered list of conversation messages.
    /// </summary>
    public IReadOnlyList<AIAgentMessage> Messages { get; init; } = Array.Empty<AIAgentMessage>();

    /// <summary>
    /// Gets the list of tool definitions available for the model to invoke.
    /// </summary>
    public IReadOnlyList<AIToolDefinition> Tools { get; init; } = Array.Empty<AIToolDefinition>();

    /// <summary>
    /// Gets optional system instructions overriding or complementing message history.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentRequest"/>.
    /// </summary>
    public AIAgentRequest(
        IEnumerable<AIAgentMessage> messages,
        IEnumerable<AIToolDefinition>? tools = null,
        string? systemPrompt = null)
    {
        Messages = messages?.ToList().AsReadOnly() ?? (IReadOnlyList<AIAgentMessage>)Array.Empty<AIAgentMessage>();
        Tools = tools?.ToList().AsReadOnly() ?? (IReadOnlyList<AIToolDefinition>)Array.Empty<AIToolDefinition>();
        SystemPrompt = systemPrompt;
    }
}

/// <summary>
/// Provider-independent interface used by the Application layer to communicate with an AI model
/// supporting function / tool calling.
/// </summary>
public interface IAIAgentProvider
{
    /// <summary>
    /// Sends conversation messages and available tool definitions to the AI provider
    /// and returns the model's response (either text output or requested tool invocations).
    /// </summary>
    /// <param name="request">The agent request containing messages and tool definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider-independent agent response.</returns>
    Task<AIAgentResponse> GenerateResponseAsync(
        AIAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload to send messages and tools directly without manually constructing <see cref="AIAgentRequest"/>.
    /// </summary>
    Task<AIAgentResponse> GenerateResponseAsync(
        IEnumerable<AIAgentMessage> messages,
        IEnumerable<AIToolDefinition>? tools = null,
        CancellationToken cancellationToken = default) =>
        GenerateResponseAsync(new AIAgentRequest(messages, tools), cancellationToken);
}
