using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Services;

/// <summary>
/// Provider-independent builder for AI agent system prompts.
/// Embeds current local date, timezone, tool discipline, truthfulness invariants, and farm context.
/// </summary>
public interface IAIAgentSystemPromptBuilder
{
    /// <summary>
    /// Builds the system prompt for an agent run with optional authenticated account context.
    /// </summary>
    /// <param name="accountContext">Optional account context built server-side.</param>
    /// <returns>Deterministic, provider-neutral system prompt string.</returns>
    string Build(AIAccountContext? accountContext);
}
