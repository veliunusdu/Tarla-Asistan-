using MediatR;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public record CreateFarmCommand(
    Guid OwnerId,
    string Name,
    double Latitude,
    double Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    CropType InitialCropType,
    DateOnly InitialPlantedAt
) : IRequest<Guid>; // Returns the new Farm's ID