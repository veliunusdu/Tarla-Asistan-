using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Notifications.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Notifications.Commands;

public class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, DeviceTokenDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPushNotificationService _pushService;

    public RegisterDeviceTokenCommandHandler(IApplicationDbContext db, IPushNotificationService pushService)
    {
        _db = db;
        _pushService = pushService;
    }

    public async Task<DeviceTokenDto> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        }

        var now = DateTime.UtcNow;
        var token = request.Token.Trim();

        var device = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == token, cancellationToken);

        if (device == null)
        {
            device = new DeviceToken
            {
                UserId = request.UserId,
                Token = token,
                Platform = request.Platform,
                Active = true,
                LastSeenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.DeviceTokens.Add(device);
        }
        else
        {
            device.UserId = request.UserId;
            device.Platform = request.Platform;
            device.Active = true;
            device.LastSeenAtUtc = now;
            device.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Dispatch any pending notifications for this user
        var pendingNotifications = await _db.Notifications
            .Where(n => n.UserId == request.UserId && n.Status == NotificationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var notification in pendingNotifications)
        {
            var sent = await _pushService.SendNotificationAsync(notification, device.Token, cancellationToken);
            if (sent)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAtUtc = DateTime.UtcNow;
            }
            else
            {
                notification.AttemptCount++;
            }
            notification.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (pendingNotifications.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return DeviceTokenDto.FromEntity(device);
    }
}
