using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.AI.Services;

public class ProactiveAdvisoryService : IProactiveAdvisoryService
{
    private readonly IApplicationDbContext _db;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IProactiveAdvisoryEngine _engine;
    private readonly IPushNotificationService? _pushService;
    private readonly ILogger<ProactiveAdvisoryService> _logger;

    public ProactiveAdvisoryService(
        IApplicationDbContext db,
        IWeatherProvider weatherProvider,
        IProactiveAdvisoryEngine engine,
        ILogger<ProactiveAdvisoryService> logger,
        IPushNotificationService? pushService = null)
    {
        _db = db;
        _weatherProvider = weatherProvider;
        _engine = engine;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProactiveAdvisoryDto>> EvaluateFarmAdvisoriesAsync(
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var farm = await _db.Farms
            .Include(f => f.CropPeriods.Where(cp => cp.Status == CropPeriodStatus.Active))
            .Include(f => f.Owner)
                .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(f => f.Id == farmId && f.ArchivedAt == null, cancellationToken);

        if (farm == null)
        {
            _logger.LogWarning("Farm {FarmId} not found for proactive evaluation.", farmId);
            return [];
        }

        if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
        {
            _logger.LogInformation("Farm {FarmId} has no GPS coordinates; skipping weather evaluation.", farmId);
            return [];
        }

        var nowUtc = DateTime.UtcNow;

        // 1. Load past activities (last 14 days)
        var pastActivities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.FarmId == farmId && a.ArchivedAtUtc == null && a.OccurredAtUtc >= nowUtc.AddDays(-14))
            .ToListAsync(cancellationToken);

        // 2. Load upcoming tasks (next 7 days)
        var upcomingTasks = await _db.FarmTasks
            .Where(t => t.FarmId == farmId && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
            .ToListAsync(cancellationToken);

        // 3. Fetch weather forecast
        WeatherForecastData? weather = null;
        try
        {
            weather = await _weatherProvider.GetWeatherAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather forecast for farm {FarmId}", farm.Id);
        }

        if (weather == null)
        {
            try
            {
                var points = await _weatherProvider.ForecastAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
                weather = new WeatherForecastData(points ?? []);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch weather forecast points for farm {FarmId}", farm.Id);
            }
        }

        // 4. Run rule engine
        var evaluationResults = _engine.Evaluate(farm, pastActivities, upcomingTasks, weather, nowUtc);
        if (evaluationResults.Count == 0)
        {
            return await GetActiveAdvisoriesAsync(farm.OwnerId, farmId, cancellationToken);
        }

        var activeCropPeriodId = farm.CropPeriods.FirstOrDefault()?.Id;

        // 5. Deduplicate and persist new advisories
        foreach (var result in evaluationResults)
        {
            var exists = await _db.ProactiveAdvisories
                .AnyAsync(a => a.DedupeKey == result.DedupeKey, cancellationToken);

            if (exists) continue;

            var advisory = new ProactiveAdvisory
            {
                FarmId = farm.Id,
                UserId = farm.OwnerId,
                CropPeriodId = activeCropPeriodId,
                RelatedTaskId = result.RelatedTaskId,
                AdvisoryType = result.AdvisoryType,
                Severity = result.Severity,
                ActionType = result.ActionType,
                Title = result.Title,
                Summary = result.Summary,
                AgronomicExplanation = result.AgronomicExplanation,
                ActionRecommendation = result.ActionRecommendation,
                RecommendedDate = result.RecommendedDate,
                MetricsJson = result.MetricsJson,
                DedupeKey = result.DedupeKey,
                ValidUntilUtc = nowUtc.AddDays(4),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _db.ProactiveAdvisories.Add(advisory);

            // 6. Notify farmer if Critical or Warning and push is enabled
            if (result.Severity is AdvisorySeverity.Critical or AdvisorySeverity.Warning &&
                _pushService != null && (farm.Owner?.Profile?.NotificationsEnabled ?? true))
            {
                await DispatchNotificationAsync(farm, advisory, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetActiveAdvisoriesAsync(farm.OwnerId, farmId, cancellationToken);
    }

    private async Task DispatchNotificationAsync(Farm farm, ProactiveAdvisory advisory, CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = farm.OwnerId,
            NotificationType = NotificationType.ProactiveAdvisory,
            Title = advisory.Title,
            Body = advisory.Summary,
            DeepLink = $"tarla-asistani://farms/{farm.Id}/advisories/{advisory.Id}",
            Data = $"{{\"farm_id\":\"{farm.Id}\",\"advisory_id\":\"{advisory.Id}\",\"severity\":\"{advisory.Severity}\"}}",
            DedupeKey = $"adv-push-{advisory.DedupeKey}",
            Status = NotificationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);

        // Persist before delivery so retries and status changes always target a durable notification.
        await _db.SaveChangesAsync(ct);

        var activeTokens = await _db.DeviceTokens
            .Where(d => d.UserId == farm.OwnerId && d.Active)
            .ToListAsync(ct);

        var anySent = false;
        foreach (var device in activeTokens)
        {
            try
            {
                var sent = await _pushService!.SendNotificationAsync(notification, device.Token, ct);
                if (sent)
                {
                    anySent = true;
                }
                else
                {
                    notification.AttemptCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push proactive notification to device {Token}", device.Token);
            }
        }

        if (anySent)
        {
            notification.Status = NotificationStatus.Sent;
            notification.SentAtUtc = DateTime.UtcNow;
        }
        notification.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProactiveAdvisoryDto>> GetActiveAdvisoriesAsync(
        Guid userId,
        Guid? farmId = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        var query = _db.ProactiveAdvisories
            .Include(a => a.Farm)
            .AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDismissed && (a.ValidUntilUtc == null || a.ValidUntilUtc > nowUtc));

        if (farmId.HasValue)
        {
            query = query.Where(a => a.FarmId == farmId.Value);
        }

        var list = await query
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return list.Select(a => new ProactiveAdvisoryDto(
            Id: a.Id,
            FarmId: a.FarmId,
            FarmName: a.Farm?.Name ?? string.Empty,
            UserId: a.UserId,
            RelatedTaskId: a.RelatedTaskId,
            AdvisoryType: a.AdvisoryType,
            Severity: a.Severity,
            ActionType: a.ActionType,
            Title: a.Title,
            Summary: a.Summary,
            AgronomicExplanation: a.AgronomicExplanation,
            ActionRecommendation: a.ActionRecommendation,
            RecommendedDate: a.RecommendedDate,
            MetricsJson: a.MetricsJson,
            IsApplied: a.IsApplied,
            IsDismissed: a.IsDismissed,
            CreatedAtUtc: a.CreatedAtUtc
        )).ToList();
    }

    public async Task<bool> ApplyAdvisoryAsync(
        Guid advisoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var advisory = await _db.ProactiveAdvisories
            .Include(a => a.RelatedTask)
            .FirstOrDefaultAsync(a => a.Id == advisoryId && a.UserId == userId, cancellationToken);

        if (advisory == null) return false;

        // Apply action to related task if applicable
        if (advisory.RelatedTask != null && advisory.RecommendedDate.HasValue)
        {
            advisory.RelatedTask.DueDate = advisory.RecommendedDate.Value;
            advisory.RelatedTask.Status = TaskStatus.Planned;
            advisory.RelatedTask.UpdatedAtUtc = DateTime.UtcNow;
        }

        advisory.IsApplied = true;
        advisory.AppliedAtUtc = DateTime.UtcNow;
        advisory.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DismissAdvisoryAsync(
        Guid advisoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var advisory = await _db.ProactiveAdvisories
            .FirstOrDefaultAsync(a => a.Id == advisoryId && a.UserId == userId, cancellationToken);

        if (advisory == null) return false;

        advisory.IsDismissed = true;
        advisory.DismissedAtUtc = DateTime.UtcNow;
        advisory.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
