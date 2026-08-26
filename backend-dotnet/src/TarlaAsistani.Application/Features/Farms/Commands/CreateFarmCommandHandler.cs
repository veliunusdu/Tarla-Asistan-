using MediatR;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class CreateFarmCommandHandler : IRequestHandler<CreateFarmCommand, Guid>
{
    private readonly ApplicationDbContext _db;

    public CreateFarmCommandHandler(ApplicationDbContext db)
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
        var cropPeriod = new CropPeriod
        {
            FarmId = farm.Id,
            CropType = request.InitialCropType,
            PlantedAt = request.InitialPlantedAt,
            Status = CropPeriodStatus.Active
        };

        _db.CropPeriods.Add(cropPeriod);

        // 3. Save everything in one database transaction
        await _db.SaveChangesAsync(cancellationToken);

        return farm.Id;
    }
}