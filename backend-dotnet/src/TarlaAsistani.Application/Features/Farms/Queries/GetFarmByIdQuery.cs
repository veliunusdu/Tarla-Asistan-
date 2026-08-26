using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;

namespace TarlaAsistani.Application.Features.Farms.Queries;

// We return FarmDto? (nullable) because the farm might not exist
public record GetFarmByIdQuery(Guid Id) : IRequest<FarmDto?>;