using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Notifications.DTOs;

namespace TarlaAsistani.Application.Features.Notifications.Queries;

public record ListNotificationsQuery(
    Guid UserId,
    bool UnreadOnly = false,
    int Limit = 50,
    int Offset = 0
) : IRequest<NotificationListDto>;

public class ListNotificationsQueryHandler : IRequestHandler<ListNotificationsQuery, NotificationListDto>
{
    private readonly IApplicationDbContext _db;

    public ListNotificationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationListDto> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Notifications.Where(n => n.UserId == request.UserId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var total = await query.CountAsync(cancellationToken);

        var unread = await _db.Notifications
            .Where(n => n.UserId == request.UserId && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);

        var limit = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(request.Offset, 0);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .Select(n => NotificationDto.FromEntity(n))
            .ToListAsync(cancellationToken);

        return new NotificationListDto(items, total, unread, limit, offset);
    }
}
