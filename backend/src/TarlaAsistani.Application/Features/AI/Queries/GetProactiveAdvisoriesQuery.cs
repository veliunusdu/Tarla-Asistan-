using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Queries;

public record GetProactiveAdvisoriesQuery(Guid UserId, Guid? FarmId = null)
    : IRequest<IReadOnlyList<ProactiveAdvisoryDto>>;

public class GetProactiveAdvisoriesQueryHandler : IRequestHandler<GetProactiveAdvisoriesQuery, IReadOnlyList<ProactiveAdvisoryDto>>
{
    private readonly IProactiveAdvisoryService _advisoryService;

    public GetProactiveAdvisoriesQueryHandler(IProactiveAdvisoryService advisoryService)
    {
        _advisoryService = advisoryService;
    }

    public async Task<IReadOnlyList<ProactiveAdvisoryDto>> Handle(
        GetProactiveAdvisoriesQuery request,
        CancellationToken cancellationToken)
    {
        return await _advisoryService.GetActiveAdvisoriesAsync(request.UserId, request.FarmId, cancellationToken);
    }
}
