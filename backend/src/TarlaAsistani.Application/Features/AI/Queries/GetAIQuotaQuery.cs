using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Queries;

public record GetAIQuotaQuery(Guid UserId) : IRequest<AIQuotaStatusDto>;

public class GetAIQuotaQueryHandler : IRequestHandler<GetAIQuotaQuery, AIQuotaStatusDto>
{
    private readonly IAIQuotaService _quotaService;

    public GetAIQuotaQueryHandler(IAIQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<AIQuotaStatusDto> Handle(GetAIQuotaQuery request, CancellationToken cancellationToken)
    {
        return _quotaService.GetQuotaStatusAsync(request.UserId, cancellationToken);
    }
}
