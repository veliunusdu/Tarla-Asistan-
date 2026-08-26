using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public class CloseCropPeriodCommandHandler : IRequestHandler<CloseCropPeriodCommand, CropPeriodDto?>
{
    private readonly IApplicationDbContext _db;

    public CloseCropPeriodCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CropPeriodDto?> Handle(CloseCropPeriodCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm existence and ownership
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null, cancellationToken);

        if (farm == null)
        {
            return null;
        }

        // 2. Find CropPeriod
        var period = await _db.CropPeriods
            .FirstOrDefaultAsync(cp => cp.Id == request.PeriodId && cp.FarmId == request.FarmId, cancellationToken);

        if (period == null)
        {
            return null;
        }

        if (period.Status != CropPeriodStatus.Active)
        {
            throw new InvalidOperationException("Bu üretim dönemi zaten kapatılmış.");
        }

        if (request.HarvestedAt < period.PlantedAt)
        {
            throw new ArgumentException("Hasat tarihi ekim tarihinden önce olamaz.");
        }

        var now = DateTime.UtcNow;
        period.HarvestedAt = request.HarvestedAt;
        period.Status = CropPeriodStatus.Archived;
        period.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return CropPeriodDto.FromEntity(period);
    }
}
