using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public class ListActivityRevisionsQueryHandler : IRequestHandler<ListActivityRevisionsQuery, List<ActivityRevisionDto>?>
{
    private readonly IApplicationDbContext _db;

    public ListActivityRevisionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ActivityRevisionDto>?> Handle(ListActivityRevisionsQuery request, CancellationToken cancellationToken)
    {
        var activityQuery = _db.Activities
            .Include(a => a.Farm)
            .Where(a => a.Id == request.ActivityId && a.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            activityQuery = activityQuery.Where(a => a.Farm.OwnerId == request.UserId);
        }

        var activityExists = await activityQuery.AnyAsync(cancellationToken);
        if (!activityExists)
        {
            return null;
        }

        var revisions = await _db.ActivityRevisions
            .Where(ar => ar.ActivityId == request.ActivityId)
            .OrderByDescending(ar => ar.ChangedAtUtc)
            .Select(ar => ActivityRevisionDto.FromEntity(ar))
            .ToListAsync(cancellationToken);

        return revisions;
    }
}
