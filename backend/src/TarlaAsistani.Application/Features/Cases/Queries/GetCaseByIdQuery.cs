using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Queries;

public record GetCaseByIdQuery(
    Guid CaseId,
    Guid UserId,
    UserRole Role
) : IRequest<CaseDetailDto?>;

public class GetCaseByIdQueryHandler : IRequestHandler<GetCaseByIdQuery, CaseDetailDto?>
{
    private readonly IApplicationDbContext _db;

    public GetCaseByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseDetailDto?> Handle(GetCaseByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SupportCases
            .Include(sc => sc.Farm)
            .Include(sc => sc.MediaLinks)
                .ThenInclude(ml => ml.Media)
            .Include(sc => sc.Messages)
                .ThenInclude(m => m.MediaLinks)
                    .ThenInclude(ml => ml.Media)
            .Include(sc => sc.Context)
            .Where(sc => sc.Id == request.CaseId && sc.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(sc => sc.Farm.OwnerId == request.UserId);
        }

        var supportCase = await query.FirstOrDefaultAsync(cancellationToken);
        return supportCase != null ? CaseDetailDto.FromEntity(supportCase) : null;
    }
}
