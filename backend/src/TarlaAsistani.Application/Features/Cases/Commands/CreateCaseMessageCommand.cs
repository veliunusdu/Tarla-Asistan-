using MediatR;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public record CreateCaseMessageCommand(
    Guid CaseId,
    Guid SenderId,
    UserRole Role,
    CaseMessageType MessageType,
    string Body,
    List<Guid>? MediaIds = null,
    Guid? ClientOperationId = null
) : IRequest<CaseMessageDto>;
