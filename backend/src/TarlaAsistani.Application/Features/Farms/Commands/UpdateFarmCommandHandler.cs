using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class UpdateFarmCommandHandler : IRequestHandler<UpdateFarmCommand, FarmMutationResultDto?>
{
    private const string DuplicateNameWarning = "Aynı ada sahip aktif bir tarlanız zaten var. Konumları kontrol edin.";
    private readonly IApplicationDbContext _db;

    public UpdateFarmCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FarmMutationResultDto?> Handle(UpdateFarmCommand request, CancellationToken cancellationToken)
    {
        var farm = await _db.Farms
            .Include(f => f.CropPeriods)
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null, cancellationToken);

        if (farm == null)
        {
            return null;
        }

        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var trimmedName = request.Name.Trim();
            var duplicateExists = await _db.Farms
                .AnyAsync(f => f.OwnerId == request.UserId &&
                               f.Id != request.FarmId &&
                               f.ArchivedAt == null &&
                               f.Name.ToLower() == trimmedName.ToLower(),
                          cancellationToken);

            if (duplicateExists)
            {
                warnings.Add(DuplicateNameWarning);
            }

            farm.Name = trimmedName;
        }

        if (request.Latitude.HasValue)
        {
            farm.Latitude = request.Latitude.Value;
        }

        if (request.Longitude.HasValue)
        {
            farm.Longitude = request.Longitude.Value;
        }

        if (request.SizeInHectares.HasValue)
        {
            farm.SizeInHectares = request.SizeInHectares.Value;
        }

        if (request.IrrigationMethod.HasValue)
        {
            farm.IrrigationMethod = request.IrrigationMethod.Value;
        }

        if (request.SoilType != null)
        {
            farm.SoilType = string.IsNullOrWhiteSpace(request.SoilType) ? null : request.SoilType.Trim();
        }

        if (request.Note != null)
        {
            farm.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        }

        farm.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var activeCrop = farm.CropPeriods
            .Where(cp => cp.Status == CropPeriodStatus.Active)
            .Select(cp => new CropPeriodDto(
                cp.Id,
                cp.FarmId,
                cp.CropName,
                cp.CropType,
                cp.Variety,
                cp.PlantedAt,
                cp.HarvestedAt,
                cp.Status,
                cp.CreatedAtUtc,
                cp.UpdatedAtUtc
            ))
            .FirstOrDefault();

        var farmDto = new FarmDto(
            farm.Id,
            farm.OwnerId,
            farm.Name,
            farm.Latitude,
            farm.Longitude,
            farm.SizeInHectares,
            farm.IrrigationMethod,
            farm.SoilType,
            farm.Note,
            farm.ArchivedAt,
            farm.CreatedAtUtc,
            farm.UpdatedAtUtc,
            activeCrop
        );

        return new FarmMutationResultDto(farmDto, warnings);
    }
}
