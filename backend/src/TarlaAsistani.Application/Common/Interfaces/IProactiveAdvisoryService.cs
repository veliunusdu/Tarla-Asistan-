using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IProactiveAdvisoryService
{
    Task<IReadOnlyList<ProactiveAdvisoryDto>> EvaluateFarmAdvisoriesAsync(Guid farmId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProactiveAdvisoryDto>> GetActiveAdvisoriesAsync(Guid userId, Guid? farmId = null, CancellationToken cancellationToken = default);

    Task<bool> ApplyAdvisoryAsync(Guid advisoryId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DismissAdvisoryAsync(Guid advisoryId, Guid userId, CancellationToken cancellationToken = default);
}
