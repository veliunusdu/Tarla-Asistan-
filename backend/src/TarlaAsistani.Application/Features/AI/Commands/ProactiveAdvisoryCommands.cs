using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public record ApplyProactiveAdvisoryCommand(Guid AdvisoryId, Guid UserId) : IRequest<bool>;

public class ApplyProactiveAdvisoryCommandHandler : IRequestHandler<ApplyProactiveAdvisoryCommand, bool>
{
    private readonly IProactiveAdvisoryService _advisoryService;

    public ApplyProactiveAdvisoryCommandHandler(IProactiveAdvisoryService advisoryService)
    {
        _advisoryService = advisoryService;
    }

    public Task<bool> Handle(ApplyProactiveAdvisoryCommand request, CancellationToken cancellationToken)
    {
        return _advisoryService.ApplyAdvisoryAsync(request.AdvisoryId, request.UserId, cancellationToken);
    }
}

public record DismissProactiveAdvisoryCommand(Guid AdvisoryId, Guid UserId) : IRequest<bool>;

public class DismissProactiveAdvisoryCommandHandler : IRequestHandler<DismissProactiveAdvisoryCommand, bool>
{
    private readonly IProactiveAdvisoryService _advisoryService;

    public DismissProactiveAdvisoryCommandHandler(IProactiveAdvisoryService advisoryService)
    {
        _advisoryService = advisoryService;
    }

    public Task<bool> Handle(DismissProactiveAdvisoryCommand request, CancellationToken cancellationToken)
    {
        return _advisoryService.DismissAdvisoryAsync(request.AdvisoryId, request.UserId, cancellationToken);
    }
}

public record EvaluateFarmAdvisoriesCommand(Guid FarmId) : IRequest<IReadOnlyList<ProactiveAdvisoryDto>>;

public class EvaluateFarmAdvisoriesCommandHandler : IRequestHandler<EvaluateFarmAdvisoriesCommand, IReadOnlyList<ProactiveAdvisoryDto>>
{
    private readonly IProactiveAdvisoryService _advisoryService;

    public EvaluateFarmAdvisoriesCommandHandler(IProactiveAdvisoryService advisoryService)
    {
        _advisoryService = advisoryService;
    }

    public Task<IReadOnlyList<ProactiveAdvisoryDto>> Handle(EvaluateFarmAdvisoriesCommand request, CancellationToken cancellationToken)
    {
        return _advisoryService.EvaluateFarmAdvisoriesAsync(request.FarmId, cancellationToken);
    }
}
