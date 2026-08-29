using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Infrastructure.Services;

public class MockPushNotificationService : IPushNotificationService
{
    private readonly ILogger<MockPushNotificationService> _logger;

    public MockPushNotificationService(ILogger<MockPushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendNotificationAsync(Notification notification, string deviceToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Push notification dispatched to device {Token}: [{Title}] {Body} ({DeepLink})",
            deviceToken, notification.Title, notification.Body, notification.DeepLink);

        return Task.FromResult(true);
    }
}
