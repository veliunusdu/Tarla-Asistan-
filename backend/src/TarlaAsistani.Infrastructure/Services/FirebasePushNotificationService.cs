using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using DomainNotification = TarlaAsistani.Domain.Entities.Notification;

namespace TarlaAsistani.Infrastructure.Services;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(IApplicationDbContext db, ILogger<FirebasePushNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> SendNotificationAsync(DomainNotification notification, string deviceToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return false;
        }

        if (FirebaseApp.DefaultInstance != null)
        {
            try
            {
                var dataDict = new Dictionary<string, string>
                {
                    ["notification_id"] = notification.Id.ToString(),
                    ["deep_link"] = notification.DeepLink ?? string.Empty
                };

                if (!string.IsNullOrWhiteSpace(notification.Data))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(notification.Data);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                dataDict[prop.Name] = prop.Value.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse notification.Data JSON: {Data}", notification.Data);
                    }
                }

                var message = new Message
                {
                    Token = deviceToken,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = notification.Title,
                        Body = notification.Body
                    },
                    Data = dataDict,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            Sound = "default",
                            ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps
                        {
                            Sound = "default",
                            ContentAvailable = true
                        }
                    }
                };

                var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
                _logger.LogInformation("FCM push notification sent successfully with response {Response}", response);
                return true;
            }
            catch (FirebaseMessagingException fme) when (fme.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                                                         fme.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                _logger.LogWarning(fme, "FCM token {TokenSnippet} is invalid or unregistered. Deactivating device token.",
                    deviceToken[..Math.Min(10, deviceToken.Length)]);

                try
                {
                    var tokenRecord = await _db.DeviceTokens
                        .FirstOrDefaultAsync(d => d.Token == deviceToken && d.Active, cancellationToken);
                    if (tokenRecord != null)
                    {
                        tokenRecord.Active = false;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to deactivate invalid FCM device token");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FCM push notification to {TokenSnippet}",
                    deviceToken[..Math.Min(10, deviceToken.Length)]);
                return false;
            }
        }

        // Mock / local fallback
        _logger.LogInformation("[DEV PUSH] Device {Token}: [{Title}] {Body} ({DeepLink})",
            deviceToken, notification.Title, notification.Body, notification.DeepLink);
        return true;
    }
}
