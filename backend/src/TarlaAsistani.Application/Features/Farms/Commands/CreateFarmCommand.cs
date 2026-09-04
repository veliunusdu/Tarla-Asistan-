using MediatR;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public record CreateFarmCommand(
    Guid OwnerId,
    string Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    string InitialCropName = "",
    CropType? InitialCropType = null,
    DateOnly InitialPlantedAt = default
) : IRequest<Guid>
{
    public CreateFarmCommand(
        Guid ownerId,
        string name,
        double? latitude,
        double? longitude,
        double? sizeInHectares,
        IrrigationMethod? irrigationMethod,
        CropType initialCropType,
        DateOnly initialPlantedAt)
        : this(ownerId, name, latitude, longitude, sizeInHectares, irrigationMethod, CropTypeHelper.ToTurkishName(initialCropType), initialCropType, initialPlantedAt)
    {
    }
}
