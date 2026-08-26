using MediatR;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public record CreateCropPeriodCommand(
    Guid FarmId,
    Guid UserId,
    CropType CropType,
    string? Variety,
    DateOnly PlantedAt,
    bool CloseExisting = false
) : IRequest<CropPeriodDto>;
