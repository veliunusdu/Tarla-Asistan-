namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Indicates the reason an AI agent invocation finished or paused.
/// </summary>
public enum AIAgentFinishReason
{
    /// <summary>The model completed its response normally.</summary>
    Stop,

    /// <summary>The model requested one or more tool executions.</summary>
    ToolCalls,

    /// <summary>The response reached token or length limit.</summary>
    Length,

    /// <summary>The model execution encountered an error or was filtered.</summary>
    Error
}

/// <summary>
/// Provider-independent result returned by an AI agent provider.
/// </summary>
public sealed record AIAgentResponse
{
    /// <summary>
    /// Gets the assistant's text response, if any.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the list of tool calls requested by the model.
    /// </summary>
    public IReadOnlyList<AIToolCall> ToolCalls { get; init; } = Array.Empty<AIToolCall>();

    /// <summary>
    /// Gets a value indicating whether the model requested one or more tool calls.
    /// </summary>
    public bool HasToolCalls => ToolCalls.Count > 0;

    /// <summary>
    /// Gets the reason the model stopped generating.
    /// </summary>
    public AIAgentFinishReason FinishReason { get; init; }

    /// <summary>
    /// Gets optional token usage statistics.
    /// </summary>
    public int? PromptTokens { get; init; }

    /// <summary>
    /// Gets optional completion token usage statistics.
    /// </summary>
    public int? CompletionTokens { get; init; }

    /// <summary>
    /// Gets optional total token usage statistics.
    /// </summary>
    public int? TotalTokens { get; init; }

    /// <summary>
    /// Gets optional provider-specific metadata (e.g. opaque thought signature or session state)
    /// preserved across conversation turns.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ProviderMetadata { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentResponse"/>.
    /// </summary>
    public AIAgentResponse(
        string? content,
        IEnumerable<AIToolCall>? toolCalls = null,
        AIAgentFinishReason finishReason = AIAgentFinishReason.Stop,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        IReadOnlyDictionary<string, string>? providerMetadata = null)
    {
        Content = content;
        ToolCalls = toolCalls?.ToList().AsReadOnly() ?? (IReadOnlyList<AIToolCall>)Array.Empty<AIToolCall>();
        FinishReason = finishReason;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens ?? (promptTokens.HasValue && completionTokens.HasValue ? promptTokens + completionTokens : null);
        ProviderMetadata = providerMetadata;
    }

    /// <summary>
    /// Creates a response representing completed text output.
    /// </summary>
    public static AIAgentResponse CreateTextResponse(
        string content,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        IReadOnlyDictionary<string, string>? providerMetadata = null) =>
        new(content, finishReason: AIAgentFinishReason.Stop, promptTokens: promptTokens, completionTokens: completionTokens, totalTokens: totalTokens, providerMetadata: providerMetadata);

    /// <summary>
    /// Creates a response representing requested tool calls.
    /// </summary>
    public static AIAgentResponse CreateToolCallsResponse(
        IEnumerable<AIToolCall> toolCalls,
        string? content = null,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        IReadOnlyDictionary<string, string>? providerMetadata = null) =>
        new(content, toolCalls: toolCalls, finishReason: AIAgentFinishReason.ToolCalls, promptTokens: promptTokens, completionTokens: completionTokens, totalTokens: totalTokens, providerMetadata: providerMetadata);

    /// <summary>
    /// Converts this response into an <see cref="AIAgentMessage"/> suitable for appending to the conversation history.
    /// </summary>
    public AIAgentMessage ToAssistantMessage() =>
        AIAgentMessage.CreateAssistant(Content, ToolCalls, providerMetadata: ProviderMetadata);
}
