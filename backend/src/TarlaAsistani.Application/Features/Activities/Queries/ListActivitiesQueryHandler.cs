using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public class ListActivitiesQueryHandler : IRequestHandler<ListActivitiesQuery, ActivityListDto>
{
    private readonly IApplicationDbContext _db;

    public ListActivitiesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityListDto> Handle(ListActivitiesQuery request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm accessibility
        var farmQuery = _db.Farms.Where(f => f.Id == request.FarmId && f.ArchivedAt == null);
        if (request.Role == UserRole.Farmer)
        {
            farmQuery = farmQuery.Where(f => f.OwnerId == request.UserId);
        }

        var farm = await farmQuery.FirstOrDefaultAsync(cancellationToken);
        if (farm == null)
        {
            throw new KeyNotFoundException("Tarla bulunamadı.");
        }

        // 2. Build Activity query with filters
        var query = _db.Activities.Where(a => a.FarmId == request.FarmId);

        if (!request.IncludeDrafts)
        {
            query = query.Where(a => a.Status == ActivityStatus.Confirmed);
        }

        if (!request.IncludeArchived)
        {
            query = query.Where(a => a.ArchivedAtUtc == null);
        }

        var total = await query.CountAsync(cancellationToken);

        var limit = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(request.Offset, 0);

        var items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .Select(a => ActivityDto.FromEntity(a))
            .ToListAsync(cancellationToken);

        return new ActivityListDto(items, total, limit, offset);
    }
}
