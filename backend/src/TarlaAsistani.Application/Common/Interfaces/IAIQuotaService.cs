using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IAIQuotaService
{
    Task CheckQuotaAsync(Guid userId, bool hasPhoto, CancellationToken cancellationToken = default);

    Task<AIQuotaStatusDto> GetQuotaStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid userId,
        string provider,
        string model,
        bool hasPhoto,
        int promptTokens,
        int completionTokens,
        long durationMs,
        decimal estimatedCostUsd,
        CancellationToken cancellationToken = default);
}
