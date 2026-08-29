using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public record ListActivityRevisionsQuery(
    Guid ActivityId,
    Guid UserId,
    UserRole Role
) : IRequest<List<ActivityRevisionDto>?>;
