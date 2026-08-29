using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Notifications.DTOs;

public record DeviceTokenDto(
    Guid Id,
    Guid UserId,
    string Token,
    DevicePlatform Platform,
    bool Active,
    DateTime LastSeenAtUtc,
    DateTime CreatedAtUtc
)
{
    public static DeviceTokenDto FromEntity(DeviceToken dt) => new(
        dt.Id,
        dt.UserId,
        dt.Token,
        dt.Platform,
        dt.Active,
        dt.LastSeenAtUtc,
        dt.CreatedAtUtc
    );
}

public record NotificationDto(
    Guid Id,
    Guid UserId,
    NotificationType NotificationType,
    string Title,
    string Body,
    string DeepLink,
    string Data,
    NotificationStatus Status,
    DateTime? SentAtUtc,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
)
{
    public static NotificationDto FromEntity(Notification n) => new(
        n.Id,
        n.UserId,
        n.NotificationType,
        n.Title,
        n.Body,
        n.DeepLink,
        n.Data,
        n.Status,
        n.SentAtUtc,
        n.ReadAtUtc,
        n.CreatedAtUtc
    );
}

public record NotificationListDto(
    List<NotificationDto> Items,
    int Total,
    int Unread,
    int Limit,
    int Offset
);
