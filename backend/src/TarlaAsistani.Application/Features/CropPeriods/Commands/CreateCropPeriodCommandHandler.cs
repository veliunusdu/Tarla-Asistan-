using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public class CreateCropPeriodCommandHandler : IRequestHandler<CreateCropPeriodCommand, CropPeriodDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCropPeriodCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CropPeriodDto> Handle(CreateCropPeriodCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm existence and ownership
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        // 2. Check for active crop period
        var activePeriod = await _db.CropPeriods
            .FirstOrDefaultAsync(cp => cp.FarmId == request.FarmId && cp.Status == CropPeriodStatus.Active, cancellationToken);

        if (activePeriod != null && !request.CloseExisting)
        {
            throw new InvalidOperationException("Bu tarlada aktif bir ürün bulunuyor. Yeni dönemi başlatmak için close_existing=true ile mevcut dönemi kapatmayı onaylayın.");
        }

        var now = DateTime.UtcNow;

        if (activePeriod != null)
        {
            if (request.PlantedAt < activePeriod.PlantedAt)
            {
                throw new ArgumentException("Yeni ekim tarihi mevcut aktif dönemin ekim tarihinden önce olamaz.");
            }

            activePeriod.Status = CropPeriodStatus.Archived;
            activePeriod.HarvestedAt = request.PlantedAt;
            activePeriod.UpdatedAtUtc = now;
        }

        // 3. Create new CropPeriod
        var cropName = !string.IsNullOrWhiteSpace(request.CropName)
            ? request.CropName.Trim()
            : (request.CropType.HasValue ? CropTypeHelper.ToTurkishName(request.CropType.Value) : string.Empty);
        var cropType = request.CropType ?? CropTypeHelper.TryMatchCanonical(cropName);

        var period = new CropPeriod
        {
            FarmId = farm.Id,
            CropName = cropName,
            CropType = cropType,
            Variety = string.IsNullOrWhiteSpace(request.Variety) ? null : request.Variety.Trim(),
            PlantedAt = request.PlantedAt,
            Status = CropPeriodStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.CropPeriods.Add(period);
        await _db.SaveChangesAsync(cancellationToken);

        return CropPeriodDto.FromEntity(period);
    }
}
