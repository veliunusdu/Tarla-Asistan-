using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Pilot.DTOs;

public record PilotFeedbackDto(
    Guid Id,
    Guid CreatedById,
    string? CreatedByName,
    FeedbackType FeedbackType,
    FeedbackStatus Status,
    int? Rating,
    string Comment,
    Guid? RelatedTaskId,
    Guid? RelatedCaseId,
    Guid? ReviewedById,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc
)
{
    public static PilotFeedbackDto FromEntity(PilotFeedback pf) => new(
        pf.Id,
        pf.CreatedById,
        pf.CreatedBy?.Profile?.FullName ?? pf.CreatedBy?.PhoneNumber,
        pf.FeedbackType,
        pf.Status,
        pf.Rating,
        pf.Comment,
        pf.RelatedTaskId,
        pf.RelatedCaseId,
        pf.ReviewedById,
        pf.ReviewedAtUtc,
        pf.CreatedAtUtc
    );
}

public record PilotFeedbackListDto(
    List<PilotFeedbackDto> Items,
    int Total,
    int Limit,
    int Offset
);

public record PilotMetricsDto(
    int WindowDays,
    int ActiveFarmers,
    int TasksCreated,
    int TasksCompleted,
    double TaskCompletionRate,
    int CriticalWeatherAlerts,
    int FalseAlerts,
    double FalseAlertRate,
    int CasesCreated,
    int CasesAnswered,
    double? AverageExpertResponseMinutes,
    int NotificationsCreated,
    int NotificationsSent,
    double NotificationDeliveryRate,
    int FeedbackCount,
    double? AverageFeedbackRating
);
