using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public class ListDailyTasksQueryHandler : IRequestHandler<ListDailyTasksQuery, DailyTaskListDto?>
{
    private static readonly TaskStatus[] ActiveStatuses = [TaskStatus.New, TaskStatus.Viewed, TaskStatus.Planned];
    private readonly IApplicationDbContext _db;

    public ListDailyTasksQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DailyTaskListDto?> Handle(ListDailyTasksQuery request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm accessibility
        var farmQuery = _db.Farms.Where(f => f.Id == request.FarmId && f.ArchivedAt == null);
        if (request.Role == UserRole.Farmer)
        {
            farmQuery = farmQuery.Where(f => f.OwnerId == request.UserId);
        }

        var farm = await farmQuery.FirstOrDefaultAsync(cancellationToken);
        if (farm == null)
        {
            return null;
        }

        // 2. Automatically generate daily tasks if today
        await TaskEngine.EnsureDailyTasksAsync(_db, farm, request.TargetDate, cancellationToken);

        // 3. Mark overdue tasks
        var overdueTasksToUpdate = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId &&
                        t.DueDate < request.TargetDate &&
                        ActiveStatuses.Contains(t.Status))
            .ToListAsync(cancellationToken);

        if (overdueTasksToUpdate.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var ot in overdueTasksToUpdate)
            {
                ot.Status = TaskStatus.Overdue;
                ot.UpdatedAtUtc = now;
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 4. Query active daily tasks for target date
        var dailyTasks = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId &&
                        t.DueDate == request.TargetDate &&
                        ActiveStatuses.Contains(t.Status))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // 5. Split critical weather alerts vs regular tasks
        var criticalWeatherAlerts = dailyTasks
            .Where(t => t.Source == TaskSource.Weather && t.Priority == TaskPriority.Critical)
            .Select(t => TaskDto.FromEntity(t))
            .ToList();

        var visibleTasks = dailyTasks
            .Where(t => !(t.Source == TaskSource.Weather && t.Priority == TaskPriority.Critical))
            .Take(3)
            .Select(t => TaskDto.FromEntity(t))
            .ToList();

        // 6. Query overdue tasks
        var overdueList = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId && t.Status == TaskStatus.Overdue)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAtUtc)
            .Take(20)
            .Select(t => TaskDto.FromEntity(t))
            .ToListAsync(cancellationToken);

        return new DailyTaskListDto(
            Date: request.TargetDate,
            Items: visibleTasks,
            CriticalWeatherAlerts: criticalWeatherAlerts,
            Overdue: overdueList,
            VisibleLimit: 3
        );
    }
}
