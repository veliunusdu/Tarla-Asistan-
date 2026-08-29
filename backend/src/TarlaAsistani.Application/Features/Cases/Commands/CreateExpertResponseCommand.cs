using MediatR;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public record CreateExpertResponseCommand(
    Guid CaseId,
    Guid ExpertId,
    UserRole Role,
    string Body,
    bool CloseCase = false,
    List<Guid>? MediaIds = null,
    Guid? ClientOperationId = null
) : IRequest<CaseDetailDto>;
