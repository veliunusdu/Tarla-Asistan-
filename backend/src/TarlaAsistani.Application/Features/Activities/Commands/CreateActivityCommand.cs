using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public record CreateActivityCommand(
    Guid FarmId,
    Guid CreatedById,
    string ActivityName = "",
    string Description = "",
    DateTime OccurredAt = default,
    ActivityType? ActivityType = null,
    Guid? CropPeriodId = null,
    ActivitySource InputMethod = ActivitySource.Manual,
    int? DurationMinutes = null,
    float? Amount = null,
    string? Unit = null,
    string? PhotoUrl = null,
    string? VoiceUrl = null,
    string? VoiceTranscript = null,
    string? PerformedBy = null,
    float? Cost = null,
    Guid? ClientOperationId = null,
    UserRole CreatedByRole = UserRole.Farmer
) : IRequest<ActivityDto>;
