using MediatR;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;

namespace TarlaAsistani.Application.Features.CropPeriods.Commands;

public record CloseCropPeriodCommand(
    Guid FarmId,
    Guid PeriodId,
    Guid UserId,
    DateOnly HarvestedAt
) : IRequest<CropPeriodDto?>;
