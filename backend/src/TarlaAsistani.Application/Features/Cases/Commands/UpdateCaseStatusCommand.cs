using MediatR;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public record UpdateCaseStatusCommand(
    Guid CaseId,
    Guid UserId,
    UserRole Role,
    CaseStatus Status,
    CasePriority? Priority = null,
    bool AssignToMe = false
) : IRequest<CaseDetailDto?>;
