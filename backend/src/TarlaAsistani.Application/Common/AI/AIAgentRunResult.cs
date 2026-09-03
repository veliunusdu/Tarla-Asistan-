namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Represents the overall outcome of an AI agent execution run.
/// </summary>
public sealed record AIAgentRunResult
{
    /// <summary>
    /// Gets a value indicating whether the agent run completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the final assistant text reply if successful.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the final provider response model if successful.
    /// </summary>
    public AIAgentResponse? Response { get; init; }

    /// <summary>
    /// Gets the full conversation history resulting from this agent execution run.
    /// </summary>
    public IReadOnlyList<AIAgentMessage> Messages { get; init; } = Array.Empty<AIAgentMessage>();

    /// <summary>
    /// Gets the machine-readable error code if the run failed.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the sanitized human-readable error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the total number of iterations (orchestrator turns) performed during this run.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Gets the total number of provider API calls dispatched during this run.
    /// </summary>
    public int ProviderCalls { get; init; }

    /// <summary>
    /// Gets the total prompt tokens accumulated across all provider calls in this run.
    /// </summary>
    public int? PromptTokens { get; init; }

    /// <summary>
    /// Gets the total completion tokens accumulated across all provider calls in this run.
    /// </summary>
    public int? CompletionTokens { get; init; }

    /// <summary>
    /// Gets the total tokens accumulated across all provider calls in this run.
    /// </summary>
    public int? TotalTokens { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentRunResult"/>.
    /// </summary>
    public AIAgentRunResult(
        bool isSuccess,
        string? content = null,
        AIAgentResponse? response = null,
        IEnumerable<AIAgentMessage>? messages = null,
        string? errorCode = null,
        string? errorMessage = null,
        int iterations = 0,
        int providerCalls = 0,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null)
    {
        IsSuccess = isSuccess;
        Content = content;
        Response = response;
        Messages = messages?.ToList().AsReadOnly() ?? (IReadOnlyList<AIAgentMessage>)Array.Empty<AIAgentMessage>();
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Iterations = iterations;
        ProviderCalls = providerCalls;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens ?? (promptTokens.HasValue && completionTokens.HasValue ? promptTokens + completionTokens : null);
    }

    /// <summary>
    /// Creates a successful agent run result with optional accumulated token usage.
    /// </summary>
    public static AIAgentRunResult Success(
        AIAgentResponse response,
        IEnumerable<AIAgentMessage> messages,
        int iterations,
        int providerCalls = 0,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new AIAgentRunResult(
            isSuccess: true,
            content: response.Content,
            response: response,
            messages: messages,
            iterations: iterations,
            providerCalls: providerCalls,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            totalTokens: totalTokens);
    }

    /// <summary>
    /// Creates a failed agent run result with safe error details and optional accumulated token usage.
    /// </summary>
    public static AIAgentRunResult Failure(
        string errorCode,
        string errorMessage,
        IEnumerable<AIAgentMessage>? messages = null,
        int iterations = 0,
        int providerCalls = 0,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new AIAgentRunResult(
            isSuccess: false,
            errorCode: errorCode,
            errorMessage: errorMessage,
            messages: messages,
            iterations: iterations,
            providerCalls: providerCalls,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            totalTokens: totalTokens);
    }
}
