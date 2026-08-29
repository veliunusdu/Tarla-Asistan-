using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Queries;

public class GetFarmByIdQueryHandler : IRequestHandler<GetFarmByIdQuery, FarmDto?>
{
    private readonly IApplicationDbContext _db;

    public GetFarmByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FarmDto?> Handle(GetFarmByIdQuery request, CancellationToken cancellationToken)
    {
        // We project directly to DTO to keep the query lightweight
        return await _db.Farms
            .AsNoTracking()
            .Where(f => f.Id == request.Id && f.ArchivedAt == null) // HIDE ARCHIVED FARMS
            .Select(f => new FarmDto(
                f.Id,
                f.OwnerId,
                f.Name,
                f.Latitude,
                f.Longitude,
                f.SizeInHectares,
                f.IrrigationMethod,
                f.SoilType,
                f.Note,
                f.ArchivedAt,
                f.CreatedAtUtc,
                f.UpdatedAtUtc,
                f.CropPeriods
                    .Where(cp => cp.Status == CropPeriodStatus.Active)
                    .Select(cp => new CropPeriodDto(
                        cp.Id,
                        cp.FarmId,
                        cp.CropType,
                        cp.Variety,
                        cp.PlantedAt,
                        cp.HarvestedAt,
                        cp.Status,
                        cp.CreatedAtUtc,
                        cp.UpdatedAtUtc
                    )).FirstOrDefault()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
