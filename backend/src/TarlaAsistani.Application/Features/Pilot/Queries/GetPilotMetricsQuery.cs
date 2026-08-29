using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Pilot.Queries;

public record GetPilotMetricsQuery(
    Guid UserId,
    UserRole Role,
    int WindowDays = 7
) : IRequest<PilotMetricsDto>;

public class GetPilotMetricsQueryHandler : IRequestHandler<GetPilotMetricsQuery, PilotMetricsDto>
{
    private readonly IApplicationDbContext _db;

    public GetPilotMetricsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PilotMetricsDto> Handle(GetPilotMetricsQuery request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist)
        {
            throw new UnauthorizedAccessException("Yalnızca uzman pilot metriklerini görüntüleyebilir.");
        }

        var windowDays = Math.Clamp(request.WindowDays, 1, 90);
        var since = DateTime.UtcNow.AddDays(-windowDays);

        // 1. Tasks
        var tasksCreated = await _db.FarmTasks
            .CountAsync(t => t.CreatedAtUtc >= since, cancellationToken);

        var tasksCompleted = await _db.FarmTasks
            .CountAsync(t => t.CreatedAtUtc >= since && t.Status == TaskStatus.Completed, cancellationToken);

        var criticalAlerts = await _db.FarmTasks
            .CountAsync(t => t.CreatedAtUtc >= since && t.Source == TaskSource.Weather && t.Priority == TaskPriority.Critical, cancellationToken);

        // 2. Feedback & False alerts
        var falseAlerts = await _db.PilotFeedbacks
            .CountAsync(pf => pf.CreatedAtUtc >= since && pf.FeedbackType == FeedbackType.FalseAlert, cancellationToken);

        var feedbackCount = await _db.PilotFeedbacks
            .CountAsync(pf => pf.CreatedAtUtc >= since, cancellationToken);

        var ratedFeedbacks = await _db.PilotFeedbacks
            .Where(pf => pf.CreatedAtUtc >= since && pf.Rating != null)
            .Select(pf => (double)pf.Rating!.Value)
            .ToListAsync(cancellationToken);

        var avgRating = ratedFeedbacks.Count > 0 ? ratedFeedbacks.Average() : (double?)null;

        // 3. Support Cases
        var cases = await _db.SupportCases
            .Where(sc => sc.CreatedAtUtc >= since)
            .ToListAsync(cancellationToken);

        var casesCreated = cases.Count;
        var casesAnswered = cases.Count(sc => sc.Status == CaseStatus.Answered || sc.Status == CaseStatus.Closed);

        // Calculate average response time
        var responseMinutes = new List<double>();
        foreach (var sc in cases)
        {
            var firstExpertMessageTime = await _db.CaseMessages
                .Include(m => m.Sender)
                .Where(m => m.CaseId == sc.Id && m.Sender != null && m.Sender.Role == UserRole.Agronomist)
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => (DateTime?)m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (firstExpertMessageTime.HasValue)
            {
                var diff = Math.Max((firstExpertMessageTime.Value - sc.CreatedAtUtc).TotalMinutes, 0);
                responseMinutes.Add(diff);
            }
        }

        // 4. Notifications
        var notificationsCreated = await _db.Notifications
            .CountAsync(n => n.CreatedAtUtc >= since, cancellationToken);

        var notificationsSent = await _db.Notifications
            .CountAsync(n => n.CreatedAtUtc >= since && n.Status == NotificationStatus.Sent, cancellationToken);

        // 5. Active Farmers
        var activeFarmersActivities = await _db.Activities
            .Include(a => a.Farm)
            .Where(a => a.CreatedAtUtc >= since && a.Farm != null)
            .Select(a => a.Farm.OwnerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeFarmersCases = await _db.SupportCases
            .Include(sc => sc.Farm)
            .Where(sc => sc.CreatedAtUtc >= since && sc.Farm != null)
            .Select(sc => sc.Farm.OwnerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeFarmersFeedback = await _db.PilotFeedbacks
            .Include(pf => pf.CreatedBy)
            .Where(pf => pf.CreatedAtUtc >= since)
            .Select(pf => pf.CreatedById)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeFarmerIds = new HashSet<Guid>(activeFarmersActivities);
        activeFarmerIds.UnionWith(activeFarmersCases);
        activeFarmerIds.UnionWith(activeFarmersFeedback);

        return new PilotMetricsDto(
            WindowDays: windowDays,
            ActiveFarmers: activeFarmerIds.Count,
            TasksCreated: tasksCreated,
            TasksCompleted: tasksCompleted,
            TaskCompletionRate: tasksCreated > 0 ? Math.Round((double)tasksCompleted / tasksCreated * 100, 2) : 0.0,
            CriticalWeatherAlerts: criticalAlerts,
            FalseAlerts: falseAlerts,
            FalseAlertRate: criticalAlerts > 0 ? Math.Round((double)falseAlerts / criticalAlerts * 100, 2) : 0.0,
            CasesCreated: casesCreated,
            CasesAnswered: casesAnswered,
            AverageExpertResponseMinutes: responseMinutes.Count > 0 ? Math.Round(responseMinutes.Average(), 2) : null,
            NotificationsCreated: notificationsCreated,
            NotificationsSent: notificationsSent,
            NotificationDeliveryRate: notificationsCreated > 0 ? Math.Round((double)notificationsSent / notificationsCreated * 100, 2) : 0.0,
            FeedbackCount: feedbackCount,
            AverageFeedbackRating: avgRating.HasValue ? Math.Round(avgRating.Value, 2) : null
        );
    }
}
