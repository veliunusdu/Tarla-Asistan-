using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.BackgroundServices;

public class AccountDeletionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AccountDeletionBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public AccountDeletionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AccountDeletionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AccountDeletionBackgroundService is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeletionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing account deletions.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingDeletionsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();

        var now = DateTime.UtcNow;

        var eligibleJobs = await db.AccountDeletionJobs
            .Include(j => j.User)
                .ThenInclude(u => u.Profile)
            .Where(j => j.Status == AccountDeletionStatus.Pending ||
                       (j.Status == AccountDeletionStatus.RetryRequired && (j.NextRetryAtUtc == null || j.NextRetryAtUtc <= now)))
            .Take(10)
            .ToListAsync(ct);

        foreach (var job in eligibleJobs)
        {
            try
            {
                job.Status = AccountDeletionStatus.Processing;
                job.ProcessingStartedAtUtc = now;
                job.AttemptCount++;
                await db.SaveChangesAsync(ct);

                var user = job.User;

                // 1. Revoke all active refresh tokens
                var activeTokens = await db.RefreshTokens
                    .Where(r => r.UserId == user.Id && r.RevokedAtUtc == null)
                    .ToListAsync(ct);

                foreach (var token in activeTokens)
                {
                    token.RevokedAtUtc = now;
                }

                // 2. Delete user's media files from storage
                var userMedia = await db.MediaAssets
                    .Where(m => m.OwnerId == user.Id)
                    .ToListAsync(ct);

                foreach (var media in userMedia)
                {
                    try
                    {
                        await storage.DeleteAsync(media.StorageKey, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete storage file {StorageKey} for user {UserId}", media.StorageKey, user.Id);
                    }
                }
                job.MediaDeletedAtUtc = now;

                // 3. Deactivate all device tokens
                var deviceTokens = await db.DeviceTokens
                    .Where(d => d.UserId == user.Id)
                    .ToListAsync(ct);

                foreach (var dt in deviceTokens)
                {
                    dt.Active = false;
                    dt.UpdatedAtUtc = now;
                }

                // 4. Anonymize user profile and credentials
                var anonId = user.AnonymizedSubjectId ?? $"anon-{Guid.NewGuid():N}";
                user.PhoneNumber = $"deleted-{anonId}";
                user.AccountStatus = AccountStatus.Anonymized;
                user.DeletedAtUtc = now;
                user.UpdatedAtUtc = now;

                if (user.Profile != null)
                {
                    user.Profile.FullName = null;
                    user.Profile.Province = null;
                    user.Profile.District = null;
                    user.Profile.NotificationsEnabled = false;
                    user.Profile.UpdatedAtUtc = now;
                }

                job.PostgresAnonymizedAtUtc = now;
                job.Status = AccountDeletionStatus.Completed;
                job.CompletedAtUtc = now;
                job.ProcessingStartedAtUtc = null;
                job.NextRetryAtUtc = null;
                job.LastErrorCode = null;

                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Account deletion completed for user {UserId}, Job {JobId}", user.Id, job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account deletion failed for job {JobId}", job.Id);
                job.Status = AccountDeletionStatus.RetryRequired;
                job.LastErrorCode = "ACCOUNT_DELETION_FAILED";
                job.NextRetryAtUtc = now.AddMinutes(30);
                job.ProcessingStartedAtUtc = null;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
