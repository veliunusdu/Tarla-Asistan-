using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Queries;

public record GetFarmsQuery(
    Guid? UserId = null,
    UserRole? Role = null,
    bool IncludeArchived = false
) : IRequest<List<FarmDto>>;
