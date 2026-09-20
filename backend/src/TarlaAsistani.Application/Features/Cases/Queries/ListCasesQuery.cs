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
    string? Scope = null,
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
        else if (request.Role == UserRole.Agronomist)
        {
            if (string.Equals(request.Scope, "pool", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sc => sc.AssignedExpertId == null && sc.Status != CaseStatus.Closed);
            }
            else if (string.Equals(request.Scope, "mine", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sc => sc.AssignedExpertId == request.UserId);
            }
            else
            {
                query = query.Where(sc => sc.AssignedExpertId == request.UserId ||
                                         (sc.AssignedExpertId == null && sc.Status != CaseStatus.Closed));
            }
        }
        else if (request.Role == UserRole.Admin)
        {
            if (string.Equals(request.Scope, "pool", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sc => sc.AssignedExpertId == null && sc.Status != CaseStatus.Closed);
            }
            else if (string.Equals(request.Scope, "mine", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sc => sc.AssignedExpertId == request.UserId);
            }
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

        var rawCases = await query
            .Include(sc => sc.Context)
            .OrderBy(sc => sc.Priority == CasePriority.Critical ? 0 :
                           sc.Priority == CasePriority.High ? 1 :
                           sc.Priority == CasePriority.Medium ? 2 : 3)
            .ThenByDescending(sc => sc.UpdatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var items = new List<CaseSummaryDto>(rawCases.Count);
        foreach (var sc in rawCases)
        {
            if (request.Role == UserRole.Agronomist && sc.AssignedExpertId != request.UserId)
            {
                items.Add(CaseSummaryDto.FromEntityRedacted(sc));
            }
            else
            {
                items.Add(CaseSummaryDto.FromEntity(sc));
            }
        }

        return new CaseListDto(items, total, limit, offset);
    }
}
