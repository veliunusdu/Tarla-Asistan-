using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, TaskDto?>
{
    private readonly IApplicationDbContext _db;

    public GetTaskByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TaskDto?> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _db.FarmTasks
            .Include(t => t.Farm)
            .Where(t => t.Id == request.TaskId && t.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(t => t.Farm.OwnerId == request.UserId);
        }

        var task = await query.FirstOrDefaultAsync(cancellationToken);
        if (task == null)
        {
            return null;
        }

        return TaskDto.FromEntity(task);
    }
}
