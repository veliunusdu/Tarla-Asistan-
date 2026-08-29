using MediatR;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Pilot.Commands;

public record CreatePilotFeedbackCommand(
    Guid CreatedById,
    UserRole Role,
    FeedbackType FeedbackType,
    string Comment,
    int? Rating = null,
    Guid? RelatedTaskId = null,
    Guid? RelatedCaseId = null
) : IRequest<PilotFeedbackDto>;
