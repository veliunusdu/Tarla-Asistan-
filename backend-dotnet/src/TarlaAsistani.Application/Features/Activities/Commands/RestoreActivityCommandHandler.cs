using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class RestoreActivityCommandHandler : IRequestHandler<RestoreActivityCommand, ActivityDto?>
{
    private readonly IApplicationDbContext _db;

    public RestoreActivityCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityDto?> Handle(RestoreActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _db.Activities
            .Include(a => a.Farm)
            .FirstOrDefaultAsync(a => a.Id == request.ActivityId && a.Farm.OwnerId == request.UserId && a.Farm.ArchivedAt == null, cancellationToken);

        if (activity == null)
        {
            return null;
        }

        if (activity.ArchivedAtUtc != null)
        {
            var now = DateTime.UtcNow;
            var revision = new ActivityRevision
            {
                ActivityId = activity.Id,
                ChangedById = request.UserId,
                PreviousValues = JsonSerializer.Serialize(new { archived_at = activity.ArchivedAtUtc.Value.ToString("o") }),
                ChangedAtUtc = now
            };

            _db.ActivityRevisions.Add(revision);

            activity.ArchivedAtUtc = null;
            activity.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);
        }

        return ActivityDto.FromEntity(activity);
    }
}
