using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Queries;

public record GetFarmSummaryQuery(
    Guid UserId,
    UserRole Role,
    int UpcomingLimit = 5
) : IRequest<FarmSummaryResponse>;
