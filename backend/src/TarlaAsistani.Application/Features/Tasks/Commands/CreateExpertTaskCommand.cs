using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public record CreateExpertTaskCommand(
    Guid FarmId,
    Guid CreatedById,
    string Title,
    string Description,
    string Reason,
    TaskPriority Priority,
    TaskConfidence Confidence,
    DateOnly DueDate,
    Guid? CropPeriodId = null,
    UserRole CreatedByRole = UserRole.Agronomist
) : IRequest<TaskDto>;
