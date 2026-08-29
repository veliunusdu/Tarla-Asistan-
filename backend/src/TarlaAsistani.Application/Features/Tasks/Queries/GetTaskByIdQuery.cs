using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public record GetTaskByIdQuery(
    Guid TaskId,
    Guid UserId,
    UserRole Role
) : IRequest<TaskDto?>;
