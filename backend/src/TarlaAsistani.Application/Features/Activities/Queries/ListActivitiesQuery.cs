using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public record ListActivitiesQuery(
    Guid FarmId,
    Guid UserId,
    UserRole Role,
    bool IncludeDrafts = true,
    bool IncludeArchived = false,
    int Limit = 50,
    int Offset = 0
) : IRequest<ActivityListDto>;
