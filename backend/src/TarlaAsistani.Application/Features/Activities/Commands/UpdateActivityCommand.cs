using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public record UpdateActivityCommand(
    Guid ActivityId,
    Guid UserId,
    string? ActivityName = null,
    ActivityType? ActivityType = null,
    string? Description = null,
    DateTime? OccurredAt = null,
    Guid? CropPeriodId = null,
    int? DurationMinutes = null,
    float? Amount = null,
    string? Unit = null,
    string? PhotoUrl = null,
    string? VoiceUrl = null,
    string? VoiceTranscript = null,
    string? PerformedBy = null,
    float? Cost = null
) : IRequest<ActivityDto?>;
