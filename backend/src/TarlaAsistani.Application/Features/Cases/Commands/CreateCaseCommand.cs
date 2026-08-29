using MediatR;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public record CreateCaseCommand(
    Guid FarmId,
    Guid CreatedById,
    CaseCategory Category,
    string Title,
    string Description,
    List<Guid>? MediaIds = null,
    Guid? ClientOperationId = null
) : IRequest<CaseDetailDto>;
