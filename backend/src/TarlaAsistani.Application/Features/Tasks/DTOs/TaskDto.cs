using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.DTOs;

public record TaskDto(
    Guid Id,
    Guid FarmId,
    Guid? CropPeriodId,
    Guid? CreatedById,
    string Title,
    string Description,
    string Reason,
    TaskPriority Priority,
    TaskStatus Status,
    TaskSource Source,
    TaskConfidence Confidence,
    DateOnly DueDate,
    string? NotAppliedReason,
    string? CompletionNote,
    string? PhotoUrl,
    DateTime? ViewedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool ExpertReviewRecommended
)
{
    public static TaskDto FromEntity(FarmTask t) => new(
        t.Id,
        t.FarmId,
        t.CropPeriodId,
        t.CreatedById,
        t.Title,
        t.Description,
        t.Reason,
        t.Priority,
        t.Status,
        t.Source,
        t.Confidence,
        t.DueDate,
        t.NotAppliedReason,
        t.CompletionNote,
        t.PhotoUrl,
        t.ViewedAtUtc,
        t.CompletedAtUtc,
        t.CreatedAtUtc,
        t.UpdatedAtUtc,
        t.ExpertReviewRecommended
    );
}

public record DailyTaskListDto(
    DateOnly Date,
    List<TaskDto> Items,
    List<TaskDto> CriticalWeatherAlerts,
    List<TaskDto> Overdue,
    int VisibleLimit = 3
);
