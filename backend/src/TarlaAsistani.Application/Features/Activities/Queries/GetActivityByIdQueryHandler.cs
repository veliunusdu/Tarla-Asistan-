using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public class GetActivityByIdQueryHandler : IRequestHandler<GetActivityByIdQuery, ActivityDto?>
{
    private readonly IApplicationDbContext _db;

    public GetActivityByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityDto?> Handle(GetActivityByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Activities
            .Include(a => a.Farm)
            .Where(a => a.Id == request.ActivityId && a.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(a => a.Farm.OwnerId == request.UserId);
        }

        var activity = await query.FirstOrDefaultAsync(cancellationToken);
        if (activity == null)
        {
            return null;
        }

        return ActivityDto.FromEntity(activity);
    }
}
