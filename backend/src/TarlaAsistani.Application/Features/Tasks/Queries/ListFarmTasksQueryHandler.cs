using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public class ListFarmTasksQueryHandler
    : IRequestHandler<ListFarmTasksQuery, List<TaskDto>?>
{
    private readonly IApplicationDbContext _db;

    public ListFarmTasksQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskDto>?> Handle(
        ListFarmTasksQuery request,
        CancellationToken cancellationToken)
    {
        var farmQuery = _db.Farms
            .Where(f => f.Id == request.FarmId && f.ArchivedAt == null);
        if (request.Role == UserRole.Farmer)
        {
            farmQuery = farmQuery.Where(f => f.OwnerId == request.UserId);
        }

        if (!await farmQuery.AnyAsync(cancellationToken))
        {
            return null;
        }

        var tasksQuery = _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId);

        if (request.Role == UserRole.Agronomist)
        {
            tasksQuery = tasksQuery.Where(t => t.CreatedById == request.UserId ||
                                              (t.CaseId != null && _db.SupportCases.Any(c => c.Id == t.CaseId && c.AssignedExpertId == request.UserId)));
        }

        return await tasksQuery
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAtUtc)
            .Select(t => TaskDto.FromEntity(t))
            .ToListAsync(cancellationToken);
    }
}
