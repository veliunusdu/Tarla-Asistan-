using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public record CompleteTaskCommand(
    Guid TaskId,
    Guid UserId,
    UserRole Role,
    string? Note = null,
    string? PhotoUrl = null
) : IRequest<TaskDto?>;
