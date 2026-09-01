using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public record ListFarmTasksQuery(
    Guid FarmId,
    Guid UserId,
    UserRole Role
) : IRequest<List<TaskDto>?>;
