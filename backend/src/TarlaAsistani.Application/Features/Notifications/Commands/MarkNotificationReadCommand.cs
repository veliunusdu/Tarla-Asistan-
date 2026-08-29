using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Notifications.DTOs;

namespace TarlaAsistani.Application.Features.Notifications.Commands;

public record MarkNotificationReadCommand(Guid NotificationId, Guid UserId) : IRequest<NotificationDto?>;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, NotificationDto?>
{
    private readonly IApplicationDbContext _db;

    public MarkNotificationReadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationDto?> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.UserId, cancellationToken);

        if (notification == null)
        {
            return null;
        }

        if (notification.ReadAtUtc == null)
        {
            var now = DateTime.UtcNow;
            notification.ReadAtUtc = now;
            notification.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NotificationDto.FromEntity(notification);
    }
}
