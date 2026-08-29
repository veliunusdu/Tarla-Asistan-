using MediatR;
using TarlaAsistani.Application.Features.Activities.DTOs;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public record ConfirmActivityCommand(
    Guid ActivityId,
    Guid UserId
) : IRequest<ActivityDto?>;
