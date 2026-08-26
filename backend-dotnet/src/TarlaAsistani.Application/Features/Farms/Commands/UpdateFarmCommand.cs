using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public record UpdateFarmCommand(
    Guid FarmId,
    Guid UserId,
    string? Name = null,
    double? Latitude = null,
    double? Longitude = null,
    double? SizeInHectares = null,
    IrrigationMethod? IrrigationMethod = null,
    string? SoilType = null,
    string? Note = null
) : IRequest<FarmMutationResultDto?>;
