using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public record GetActivityByIdQuery(
    Guid ActivityId,
    Guid UserId,
    UserRole Role
) : IRequest<ActivityDto?>;
