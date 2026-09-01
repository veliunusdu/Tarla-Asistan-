using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Queries;

// We return FarmDto? (nullable) because the farm might not exist or user might not have access
public record GetFarmByIdQuery(
    Guid Id,
    Guid? UserId = null,
    UserRole? Role = null
) : IRequest<FarmDto?>;