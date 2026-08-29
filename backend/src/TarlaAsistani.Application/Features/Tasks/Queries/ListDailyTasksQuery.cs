using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public record ListDailyTasksQuery(
    Guid FarmId,
    Guid UserId,
    UserRole Role,
    DateOnly TargetDate
) : IRequest<DailyTaskListDto?>;
