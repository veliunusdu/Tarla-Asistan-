using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Common.Interfaces;

/// <summary>
/// Builds the per-request AI account context from the authenticated user's data.
/// Always queries the authoritative database — never trusts client-supplied values.
/// </summary>
public interface IAIContextService
{
    /// <summary>
    /// Loads the authenticated user's profile + farm summaries + (optionally) weather
    /// for weather-relevant requests.
    /// </summary>
    /// <param name="userId">Authenticated user id from JWT ClaimsPrincipal.</param>
    /// <param name="message">User message text — used for weather-intent detection.</param>
    /// <param name="hintFarmId">
    /// Optional hint from mobile client. Validated against ownership before use.
    /// If the farm does not belong to userId it is silently ignored.
    /// </param>
    /// <param name="cancellationToken"/>
    Task<AIAccountContext> BuildContextAsync(
        Guid userId,
        string message,
        Guid? hintFarmId,
        CancellationToken cancellationToken = default);
}
