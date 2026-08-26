using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.DTOs;

public record ActivityDto(
    Guid Id,
    Guid FarmId,
    Guid? CropPeriodId,
    Guid? TaskId,
    Guid? CreatedById,
    ActivityType ActivityType,
    ActivityStatus Status,
    ActivitySource Source,
    string Description,
    DateTime OccurredAtUtc,
    int? DurationMinutes,
    float? Amount,
    string? Unit,
    string? PhotoUrl,
    string? VoiceUrl,
    string? VoiceTranscript,
    string? PerformedBy,
    float? Cost,
    DateTime? ConfirmedAtUtc,
    DateTime? ArchivedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
)
{
    public static ActivityDto FromEntity(Activity a) => new(
        a.Id,
        a.FarmId,
        a.CropPeriodId,
        a.TaskId,
        a.CreatedById,
        a.ActivityType,
        a.Status,
        a.Source,
        a.Description,
        a.OccurredAtUtc,
        a.DurationMinutes,
        a.Amount,
        a.Unit,
        a.PhotoUrl,
        a.VoiceUrl,
        a.VoiceTranscript,
        a.PerformedBy,
        a.Cost,
        a.ConfirmedAtUtc,
        a.ArchivedAtUtc,
        a.CreatedAtUtc,
        a.UpdatedAtUtc
    );
}

public record ActivityListDto(
    List<ActivityDto> Items,
    int Total,
    int Limit,
    int Offset
);
