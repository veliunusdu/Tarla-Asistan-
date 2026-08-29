using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, TaskDto?>
{
    private static readonly TaskStatus[] TerminalStatuses = [TaskStatus.Completed, TaskStatus.NotApplied, TaskStatus.Cancelled];
    private readonly IApplicationDbContext _db;

    public UpdateTaskStatusCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TaskDto?> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch Task with Farm ownership check
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

        // 2. Role permission check
        if (request.Role == UserRole.Agronomist && request.Status != TaskStatus.Cancelled)
        {
            throw new UnauthorizedAccessException("Uzman yalnızca görevi iptal edebilir.");
        }

        if (request.Role == UserRole.Farmer && request.Status == TaskStatus.Cancelled)
        {
            throw new UnauthorizedAccessException("Görevi yalnızca uzman iptal edebilir.");
        }

        // 3. Terminal status check
        if (TerminalStatuses.Contains(task.Status))
        {
            if (task.Status == request.Status)
            {
                return TaskDto.FromEntity(task);
            }

            throw new InvalidOperationException("Sonlandırılmış görevin durumu değiştirilemez.");
        }

        var now = DateTime.UtcNow;
        task.Status = request.Status;
        task.CompletionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        task.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim();
        task.UpdatedAtUtc = now;

        if (request.Status == TaskStatus.Viewed)
        {
            task.ViewedAtUtc = now;
        }
        else if (request.Status == TaskStatus.Completed)
        {
            task.CompletedAtUtc = now;

            // Check if activity already exists for this task completion
            var activityExists = await _db.Activities
                .AnyAsync(a => a.TaskId == task.Id, cancellationToken);

            if (!activityExists)
            {
                var detail = task.CompletionNote ?? task.Description;
                var activity = new Activity
                {
                    FarmId = task.FarmId,
                    CropPeriodId = task.CropPeriodId,
                    TaskId = task.Id,
                    CreatedById = request.UserId,
                    ActivityType = ActivityType.Other,
                    Status = ActivityStatus.Confirmed,
                    Source = ActivitySource.Task,
                    Description = $"Görev tamamlandı: {task.Title}. {detail}",
                    OccurredAtUtc = now,
                    PhotoUrl = task.PhotoUrl,
                    ConfirmedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _db.Activities.Add(activity);
            }
        }
        else if (request.Status == TaskStatus.NotApplied)
        {
            task.NotAppliedReason = string.IsNullOrWhiteSpace(request.NotAppliedReason) ? null : request.NotAppliedReason.Trim();
            task.CompletedAtUtc = now;
        }
        else if (request.Status == TaskStatus.Cancelled)
        {
            task.CompletedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return TaskDto.FromEntity(task);
    }
}
