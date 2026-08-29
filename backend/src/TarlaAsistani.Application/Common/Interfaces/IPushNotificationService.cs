using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IPushNotificationService
{
    Task<bool> SendNotificationAsync(Notification notification, string deviceToken, CancellationToken cancellationToken = default);
}
