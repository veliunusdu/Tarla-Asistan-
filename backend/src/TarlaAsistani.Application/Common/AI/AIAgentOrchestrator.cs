using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Orchestrates the multi-turn agent interaction loop between <see cref="IAIAgentProvider"/> and <see cref="IAgentToolRegistry"/>.
/// Manages conversation state, executes tool calls sequentially, handles tool failures safely, and enforces iteration limits.
/// </summary>
public class AIAgentOrchestrator : IAIAgentOrchestrator
{
    private readonly IAIAgentProvider _agentProvider;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly AIAgentOrchestratorOptions _options;
    private readonly ILogger<AIAgentOrchestrator>? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentOrchestrator"/> with DI options.
    /// </summary>
    public AIAgentOrchestrator(
        IAIAgentProvider agentProvider,
        IAgentToolRegistry toolRegistry,
        IOptions<AIAgentOrchestratorOptions>? options = null,
        ILogger<AIAgentOrchestrator>? logger = null)
        : this(agentProvider, toolRegistry, options?.Value ?? new AIAgentOrchestratorOptions(), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentOrchestrator"/> with explicit options.
    /// </summary>
    public AIAgentOrchestrator(
        IAIAgentProvider agentProvider,
        IAgentToolRegistry toolRegistry,
        AIAgentOrchestratorOptions options,
        ILogger<AIAgentOrchestrator>? logger = null)
    {
        _agentProvider = agentProvider ?? throw new ArgumentNullException(nameof(agentProvider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _options = options ?? new AIAgentOrchestratorOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<AIAgentRunResult> RunAsync(
        AIAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunAsync(request.Messages, request.SystemPrompt, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AIAgentRunResult> RunAsync(
        string userMessage,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(new[] { AIAgentMessage.CreateUser(userMessage) }, systemPrompt, cancellationToken);

    /// <inheritdoc />
    public async Task<AIAgentRunResult> RunAsync(
        IEnumerable<AIAgentMessage> messages,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var conversation = messages.ToList();
        var tools = _toolRegistry.GetToolDefinitions();
        var maxIterations = _options.MaxIterations;
        var iteration = 0;
        var providerCalls = 0;
        int? totalPromptTokens = null;
        int? totalCompletionTokens = null;
        int? totalTokens = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iteration++;

            if (iteration > maxIterations)
            {
                _logger?.LogWarning(
                    "AI Agent reached maximum configured iterations limit ({MaxIterations}). Stopping loop.",
                    maxIterations);

                return AIAgentRunResult.Failure(
                    errorCode: "agent_max_iterations_exceeded",
                    errorMessage: "The agent reached the maximum number of tool execution iterations without completing.",
                    messages: conversation,
                    iterations: iteration - 1,
                    providerCalls: providerCalls,
                    promptTokens: totalPromptTokens,
                    completionTokens: totalCompletionTokens,
                    totalTokens: totalTokens);
            }

            AIAgentResponse response;
            try
            {
                var request = new AIAgentRequest(conversation, tools, systemPrompt);
                response = await _agentProvider.GenerateResponseAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Propagate intentional cancellation requested by caller
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AI Agent provider encountered an unexpected error during iteration {Iteration}.", iteration);

                return AIAgentRunResult.Failure(
                    errorCode: "agent_provider_error",
                    errorMessage: "An error occurred while communicating with the AI service.",
                    messages: conversation,
                    iterations: iteration,
                    providerCalls: providerCalls,
                    promptTokens: totalPromptTokens,
                    completionTokens: totalCompletionTokens,
                    totalTokens: totalTokens);
            }

            providerCalls++;
            if (response.PromptTokens.HasValue)
            {
                totalPromptTokens = (totalPromptTokens ?? 0) + response.PromptTokens.Value;
            }
            if (response.CompletionTokens.HasValue)
            {
                totalCompletionTokens = (totalCompletionTokens ?? 0) + response.CompletionTokens.Value;
            }
            if (response.TotalTokens.HasValue)
            {
                totalTokens = (totalTokens ?? 0) + response.TotalTokens.Value;
            }
            else if (response.PromptTokens.HasValue || response.CompletionTokens.HasValue)
            {
                totalTokens = (totalTokens ?? 0) + (response.PromptTokens ?? 0) + (response.CompletionTokens ?? 0);
            }

            // Edge Case: Provider returned neither tool calls nor assistant text
            if (!response.HasToolCalls && string.IsNullOrWhiteSpace(response.Content))
            {
                _logger?.LogWarning("AI Agent provider returned an empty response with no content and no tool calls.");

                return AIAgentRunResult.Failure(
                    errorCode: "agent_empty_response",
                    errorMessage: "The AI service returned an empty response.",
                    messages: conversation,
                    iterations: iteration,
                    providerCalls: providerCalls,
                    promptTokens: totalPromptTokens,
                    completionTokens: totalCompletionTokens,
                    totalTokens: totalTokens);
            }

            // Normal completion: model generated a final textual reply with no tool calls
            if (!response.HasToolCalls)
            {
                conversation.Add(response.ToAssistantMessage());
                return AIAgentRunResult.Success(
                    response,
                    conversation,
                    iteration,
                    providerCalls: providerCalls,
                    promptTokens: totalPromptTokens,
                    completionTokens: totalCompletionTokens,
                    totalTokens: totalTokens);
            }

            // Model requested one or more tool calls.
            // If we have reached maxIterations, we cannot execute tools and run another provider call.
            if (iteration >= maxIterations)
            {
                _logger?.LogWarning(
                    "AI Agent reached maximum iteration limit ({MaxIterations}) when model requested additional tool calls.",
                    maxIterations);

                return AIAgentRunResult.Failure(
                    errorCode: "agent_max_iterations_exceeded",
                    errorMessage: "The agent reached the maximum number of tool execution iterations without completing.",
                    messages: conversation,
                    iterations: iteration,
                    providerCalls: providerCalls,
                    promptTokens: totalPromptTokens,
                    completionTokens: totalCompletionTokens,
                    totalTokens: totalTokens);
            }

            // 1. Preserve conversation ordering: Append assistant turn BEFORE tool results
            conversation.Add(response.ToAssistantMessage());

            // 2. Execute multiple tool calls sequentially in returned order
            foreach (var toolCall in response.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var toolResult = await _toolRegistry.ExecuteToolAsync(toolCall, cancellationToken);

                // 3. Append tool result message to conversation history
                conversation.Add(AIAgentMessage.CreateToolResult(toolResult));
            }
        }
    }
}
