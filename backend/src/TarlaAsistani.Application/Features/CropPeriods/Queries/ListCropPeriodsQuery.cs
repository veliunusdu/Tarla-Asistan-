using MediatR;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.CropPeriods.Queries;

public record ListCropPeriodsQuery(
    Guid FarmId,
    Guid UserId,
    UserRole Role
) : IRequest<CropPeriodListDto?>;
