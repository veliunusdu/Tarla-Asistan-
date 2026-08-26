using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Queries;

public class GetFarmsQueryHandler : IRequestHandler<GetFarmsQuery, List<FarmDto>>
{
    private readonly IApplicationDbContext _db;

    public GetFarmsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<FarmDto>> Handle(GetFarmsQuery request, CancellationToken cancellationToken)
    {
        // AsNoTracking() improves performance for read-only queries
        // Include() loads the related CropPeriods
        return await _db.Farms
            .AsNoTracking()
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
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
