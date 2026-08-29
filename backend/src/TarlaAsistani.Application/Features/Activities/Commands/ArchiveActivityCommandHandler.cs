using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class ArchiveActivityCommandHandler : IRequestHandler<ArchiveActivityCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public ArchiveActivityCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(ArchiveActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _db.Activities
            .Include(a => a.Farm)
            .FirstOrDefaultAsync(a => a.Id == request.ActivityId && a.Farm.OwnerId == request.UserId && a.ArchivedAtUtc == null && a.Farm.ArchivedAt == null, cancellationToken);

        if (activity == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var revision = new ActivityRevision
        {
            ActivityId = activity.Id,
            ChangedById = request.UserId,
            PreviousValues = "{\"archived_at\":null}",
            ChangedAtUtc = now
        };

        _db.ActivityRevisions.Add(revision);

        activity.ArchivedAtUtc = now;
        activity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
