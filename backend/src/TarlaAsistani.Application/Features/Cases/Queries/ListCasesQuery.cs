using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Queries;

public record ListCasesQuery(
    Guid UserId,
    UserRole Role,
    CaseStatus? Status = null,
    CasePriority? Priority = null,
    Guid? FarmId = null,
    int Limit = 50,
    int Offset = 0
) : IRequest<CaseListDto>;

public class ListCasesQueryHandler : IRequestHandler<ListCasesQuery, CaseListDto>
{
    private readonly IApplicationDbContext _db;

    public ListCasesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseListDto> Handle(ListCasesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SupportCases
            .Include(sc => sc.Farm)
            .Include(sc => sc.Messages)
            .Include(sc => sc.MediaLinks)
            .Where(sc => sc.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(sc => sc.Farm.OwnerId == request.UserId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(sc => sc.Status == request.Status.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(sc => sc.Priority == request.Priority.Value);
        }

        if (request.FarmId.HasValue)
        {
            query = query.Where(sc => sc.FarmId == request.FarmId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var limit = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(request.Offset, 0);

        var items = await query
            .OrderBy(sc => sc.Priority == CasePriority.Critical ? 0 :
                           sc.Priority == CasePriority.High ? 1 :
                           sc.Priority == CasePriority.Medium ? 2 : 3)
            .ThenByDescending(sc => sc.UpdatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .Select(sc => new CaseSummaryDto(
                sc.Id,
                sc.FarmId,
                sc.Farm.Name,
                sc.CreatedById,
                sc.AssignedExpertId,
                sc.Category,
                sc.Priority,
                sc.Status,
                sc.Title,
                sc.CreatedAtUtc,
                sc.UpdatedAtUtc,
                sc.ClosedAtUtc,
                sc.Messages.Count,
                sc.MediaLinks.Count
            ))
            .ToListAsync(cancellationToken);

        return new CaseListDto(items, total, limit, offset);
    }
}
