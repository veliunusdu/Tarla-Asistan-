using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.DTOs;

public record CropPeriodDto(
    Guid Id,
    Guid FarmId,
    string CropName,
    CropType? CropType,
    string? Variety,
    DateOnly PlantedAt,
    DateOnly? HarvestedAt,
    CropPeriodStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
)
{
    public static CropPeriodDto FromEntity(CropPeriod cp) => new(
        cp.Id,
        cp.FarmId,
        !string.IsNullOrWhiteSpace(cp.CropName) ? cp.CropName : (cp.CropType.HasValue ? CropTypeHelper.ToTurkishName(cp.CropType.Value) : string.Empty),
        cp.CropType,
        cp.Variety,
        cp.PlantedAt,
        cp.HarvestedAt,
        cp.Status,
        cp.CreatedAtUtc,
        cp.UpdatedAtUtc
    );
}

public record CropPeriodListDto(
    List<CropPeriodDto> Items
);
