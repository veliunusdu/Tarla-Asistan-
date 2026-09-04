using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class CreateFarmCommandHandler : IRequestHandler<CreateFarmCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateFarmCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> Handle(CreateFarmCommand request, CancellationToken cancellationToken)
    {
        // 1. Create the Farm
        var farm = new Farm
        {
            OwnerId = request.OwnerId,
            Name = request.Name,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SizeInHectares = request.SizeInHectares,
            IrrigationMethod = request.IrrigationMethod
        };

        _db.Farms.Add(farm);

        // 2. Create the Initial Crop Period (Business Rule from Python backend)
        var cropName = !string.IsNullOrWhiteSpace(request.InitialCropName)
            ? request.InitialCropName.Trim()
            : (request.InitialCropType.HasValue ? CropTypeHelper.ToTurkishName(request.InitialCropType.Value) : string.Empty);
        var cropType = request.InitialCropType ?? CropTypeHelper.TryMatchCanonical(cropName);

        var cropPeriod = new CropPeriod
        {
            FarmId = farm.Id,
            CropName = cropName,
            CropType = cropType,
            PlantedAt = request.InitialPlantedAt,
            Status = CropPeriodStatus.Active
        };

        _db.CropPeriods.Add(cropPeriod);

        // 3. Save everything in one database transaction
        await _db.SaveChangesAsync(cancellationToken);

        return farm.Id;
    }
}