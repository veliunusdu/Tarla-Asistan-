namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Orchestrates the multi-turn AI tool-calling loop between <see cref="IAIAgentProvider"/> and <see cref="IAgentToolRegistry"/>.
/// </summary>
public interface IAIAgentOrchestrator
{
    /// <summary>
    /// Executes the agent loop for the given conversation messages and optional system prompt until a final response is generated.
    /// Available tool definitions are retrieved directly from <see cref="IAgentToolRegistry"/>.
    /// </summary>
    /// <param name="messages">The initial conversation messages.</param>
    /// <param name="systemPrompt">Optional system prompt/instructions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the agent run.</returns>
    Task<AIAgentRunResult> RunAsync(
        IEnumerable<AIAgentMessage> messages,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the agent loop for the given request.
    /// Available tool definitions are supplemented from <see cref="IAgentToolRegistry"/> if not already present.
    /// </summary>
    /// <param name="request">The agent request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the agent run.</returns>
    Task<AIAgentRunResult> RunAsync(
        AIAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience helper to execute the agent loop starting from a single user message.
    /// </summary>
    Task<AIAgentRunResult> RunAsync(
        string userMessage,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { AIAgentMessage.CreateUser(userMessage) }, systemPrompt, cancellationToken);
}
