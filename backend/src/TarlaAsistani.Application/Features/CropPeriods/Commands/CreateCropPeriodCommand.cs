using MediatR;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public record CreateCropPeriodCommand(
    Guid FarmId,
    Guid UserId,
    string CropName = "",
    CropType? CropType = null,
    string? Variety = null,
    DateOnly PlantedAt = default,
    bool CloseExisting = false
) : IRequest<CropPeriodDto>
{
    public CreateCropPeriodCommand(
        Guid farmId,
        Guid userId,
        CropType cropType,
        string? variety,
        DateOnly plantedAt,
        bool closeExisting = false)
        : this(farmId, userId, CropTypeHelper.ToTurkishName(cropType), cropType, variety, plantedAt, closeExisting)
    {
    }
}
