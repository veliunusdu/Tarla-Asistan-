using MediatR;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public record ArchiveActivityCommand(
    Guid ActivityId,
    Guid UserId
) : IRequest<bool>;
