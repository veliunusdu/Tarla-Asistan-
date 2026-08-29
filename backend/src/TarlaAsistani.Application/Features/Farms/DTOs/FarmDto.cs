using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.DTOs;

// The shape of the farm data returned to the client
public record FarmDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    string? SoilType,
    string? Note,
    DateTime? ArchivedAt,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    CropPeriodDto? CurrentCropPeriod
);

// The shape of the crop period data
public record CropPeriodDto(
    Guid Id,
    Guid FarmId,
    CropType CropType,
    string? Variety,
    DateOnly PlantedAt,
    DateOnly? HarvestedAt,
    CropPeriodStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record FarmMutationResultDto(
    FarmDto Farm,
    List<string> Warnings
);