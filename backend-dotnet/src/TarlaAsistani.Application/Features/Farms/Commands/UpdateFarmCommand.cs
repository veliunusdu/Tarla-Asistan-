using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public record UpdateFarmCommand(
    Guid FarmId,
    Guid UserId,
    string? Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    string? SoilType,
    string? Note
) : IRequest<FarmMutationResultDto?>;
