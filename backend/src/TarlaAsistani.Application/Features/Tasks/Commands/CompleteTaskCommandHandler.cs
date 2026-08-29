using MediatR;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class CompleteTaskCommandHandler : IRequestHandler<CompleteTaskCommand, TaskDto?>
{
    private readonly IMediator _mediator;

    public CompleteTaskCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<TaskDto?> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var command = new UpdateTaskStatusCommand(
            TaskId: request.TaskId,
            UserId: request.UserId,
            Role: request.Role,
            Status: TaskStatus.Completed,
            NotAppliedReason: null,
            Note: request.Note,
            PhotoUrl: request.PhotoUrl
        );

        return await _mediator.Send(command, cancellationToken);
    }
}
