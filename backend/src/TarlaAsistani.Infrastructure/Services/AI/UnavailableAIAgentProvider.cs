using TarlaAsistani.Application.Common.AI;

namespace TarlaAsistani.Infrastructure.Services.AI;

/// <summary>
/// Safe placeholder implementation of <see cref="IAIAgentProvider"/> used when no remote agent provider is active
/// (e.g. when configured with the passive 'local' chat provider).
/// Prevents accidental fabrication of tool calls or writes.
/// </summary>
public class UnavailableAIAgentProvider : IAIAgentProvider
{
    public Task<AIAgentResponse> GenerateResponseAsync(AIAgentRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI agent provider is unavailable for the current provider configuration ('local').");
    }
}
