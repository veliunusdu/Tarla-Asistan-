using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class ConfirmActivityCommandHandler : IRequestHandler<ConfirmActivityCommand, ActivityDto?>
{
    private readonly IApplicationDbContext _db;

    public ConfirmActivityCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityDto?> Handle(ConfirmActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _db.Activities
            .Include(a => a.Farm)
            .FirstOrDefaultAsync(a => a.Id == request.ActivityId && a.Farm.OwnerId == request.UserId && a.ArchivedAtUtc == null && a.Farm.ArchivedAt == null, cancellationToken);

        if (activity == null)
        {
            return null;
        }

        if (activity.Status == ActivityStatus.Confirmed)
        {
            return ActivityDto.FromEntity(activity);
        }

        var now = DateTime.UtcNow;
        activity.Status = ActivityStatus.Confirmed;
        activity.ConfirmedAtUtc = now;
        activity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return ActivityDto.FromEntity(activity);
    }
}
