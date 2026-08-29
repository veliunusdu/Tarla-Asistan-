using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public record UpdateTaskStatusCommand(
    Guid TaskId,
    Guid UserId,
    UserRole Role,
    TaskStatus Status,
    string? NotAppliedReason = null,
    string? Note = null,
    string? PhotoUrl = null
) : IRequest<TaskDto?>;
