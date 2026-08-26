using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public record GetFarmJournalQuery(
    Guid FarmId,
    Guid UserId,
    UserRole Role,
    int Limit = 50,
    int Offset = 0
) : IRequest<FarmJournalResponseDto?>;
