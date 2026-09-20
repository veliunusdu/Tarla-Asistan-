using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

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

        var supportCase = await query.FirstOrDefaultAsync(cancellationToken);
        if (supportCase == null)
        {
            return null;
        }

        if (request.Role == UserRole.Farmer)
        {
            if (supportCase.Farm.OwnerId != request.UserId)
            {
                return null;
            }
            return CaseDetailDto.FromEntity(supportCase);
        }

        if (request.Role == UserRole.Agronomist)
        {
            // Case assigned to another expert -> Forbidden
            if (supportCase.AssignedExpertId.HasValue && supportCase.AssignedExpertId.Value != request.UserId)
            {
                throw new ForbiddenException("Bu vaka başka bir uzmana atanmıştır.");
            }

            // Case unassigned in pool -> Redacted view
            if (!supportCase.AssignedExpertId.HasValue)
            {
                return CaseDetailDto.FromEntityRedacted(supportCase);
            }

            // Assigned to this expert -> Full view
            return CaseDetailDto.FromEntity(supportCase);
        }

        // Admin -> Full view
        return CaseDetailDto.FromEntity(supportCase);
    }
}
