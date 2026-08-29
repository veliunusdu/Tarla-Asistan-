using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Queries;

public class ListCropPeriodsQueryHandler : IRequestHandler<ListCropPeriodsQuery, CropPeriodListDto?>
{
    private readonly IApplicationDbContext _db;

    public ListCropPeriodsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CropPeriodListDto?> Handle(ListCropPeriodsQuery request, CancellationToken cancellationToken)
    {
        var farmQuery = _db.Farms.Where(f => f.Id == request.FarmId && f.ArchivedAt == null);
        if (request.Role == UserRole.Farmer)
        {
            farmQuery = farmQuery.Where(f => f.OwnerId == request.UserId);
        }

        var farm = await farmQuery.FirstOrDefaultAsync(cancellationToken);
        if (farm == null)
        {
            return null;
        }

        var periods = await _db.CropPeriods
            .Where(cp => cp.FarmId == request.FarmId)
            .OrderByDescending(cp => cp.PlantedAt)
            .ThenByDescending(cp => cp.CreatedAtUtc)
            .Select(cp => CropPeriodDto.FromEntity(cp))
            .ToListAsync(cancellationToken);

        return new CropPeriodListDto(periods);
    }
}
