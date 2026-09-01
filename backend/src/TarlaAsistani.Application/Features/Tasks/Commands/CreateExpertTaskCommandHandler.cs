using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class CreateExpertTaskCommandHandler : IRequestHandler<CreateExpertTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPushNotificationService? _pushService;
    private readonly ILogger<CreateExpertTaskCommandHandler> _logger;

    public CreateExpertTaskCommandHandler(
        IApplicationDbContext db,
        IPushNotificationService? pushService = null,
        ILogger<CreateExpertTaskCommandHandler>? logger = null)
    {
        _db = db;
        _pushService = pushService;
        _logger = logger ?? NullLogger<CreateExpertTaskCommandHandler>.Instance;
    }

    public async Task<TaskDto> Handle(CreateExpertTaskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating expert task for farm {FarmId}, title: {Title}", request.FarmId, request.Title);

        // 1. Verify Farm exists and is not archived
        var farm = await _db.Farms
            .Include(f => f.Owner)
                .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.ArchivedAt == null, cancellationToken)
            ?? throw new FarmNotFoundException(request.FarmId);

        if (request.CreatedByRole == UserRole.Farmer && farm.OwnerId != request.CreatedById)
        {
            throw new UnauthorizedAccessException("Yalnızca kendi tarlanıza görev ekleyebilirsiniz.");
        }

        // 2. Resolve CropPeriodId
        Guid? resolvedCropPeriodId = request.CropPeriodId;
        if (resolvedCropPeriodId == null)
        {
            var activeCrop = await _db.CropPeriods
                .FirstOrDefaultAsync(cp => cp.FarmId == request.FarmId && cp.Status == CropPeriodStatus.Active, cancellationToken);
            resolvedCropPeriodId = activeCrop?.Id;
        }
        else
        {
            var cropExists = await _db.CropPeriods
                .AnyAsync(cp => cp.Id == resolvedCropPeriodId.Value && cp.FarmId == request.FarmId, cancellationToken);

            if (!cropExists)
            {
                throw new CropPeriodMismatchException(resolvedCropPeriodId.Value, request.FarmId);
            }
        }

        // 3. Generate DedupeKey
        var source = request.CreatedByRole == UserRole.Farmer ? TaskSource.Manual : TaskSource.Expert;
        var dedupeRaw = $"{source}|{request.Title.Trim()}|{request.Description.Trim()}|{request.Reason.Trim()}|{resolvedCropPeriodId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(dedupeRaw));
        var dedupeKey = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (dedupeKey.Length > 64)
        {
            dedupeKey = dedupeKey[..64];
        }

        // 4. Create FarmTask with Transactional Integrity
        using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var task = new FarmTask
            {
                FarmId = farm.Id,
                CropPeriodId = resolvedCropPeriodId,
                CreatedById = request.CreatedById,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Reason = request.Reason.Trim(),
                Priority = request.Priority,
                Status = TaskStatus.New,
                Source = source,
                Confidence = request.Confidence,
                DueDate = request.DueDate,
                DedupeKey = dedupeKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.FarmTasks.Add(task);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new DuplicateTaskException(dedupeKey, isKey: true);
            }

            // 5. Notify farm owner of assigned task
            if (source == TaskSource.Expert && _pushService != null && (farm.Owner?.Profile?.NotificationsEnabled ?? true))
            {
                var notification = new Notification
                {
                    UserId = farm.OwnerId,
                    NotificationType = NotificationType.TaskAssigned,
                    Title = "Yeni uzman göreviniz var",
                    Body = task.Title,
                    DeepLink = $"tarla-asistani://farms/{farm.Id}/tasks/{task.Id}",
                    Data = $"{{\"farm_id\":\"{farm.Id}\",\"task_id\":\"{task.Id}\"}}",
                    DedupeKey = $"task-assigned:{task.Id}",
                    Status = NotificationStatus.Pending,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync(cancellationToken);

                var activeTokens = await _db.DeviceTokens
                    .Where(d => d.UserId == farm.OwnerId && d.Active)
                    .ToListAsync(cancellationToken);

                if (activeTokens.Count > 0)
                {
                    _logger.LogInformation("Dispatching push notifications in parallel for task {TaskId} to {Count} active devices", task.Id, activeTokens.Count);

                    var sendTasks = activeTokens.Select(async device =>
                    {
                        var sent = await _pushService.SendNotificationAsync(notification, device.Token, cancellationToken);
                        return (device, sent);
                    });

                    var results = await Task.WhenAll(sendTasks);

                    var anySent = false;
                    foreach (var (device, sent) in results)
                    {
                        if (sent)
                        {
                            anySent = true;
                        }
                        else
                        {
                            notification.AttemptCount++;
                        }
                    }

                    if (anySent)
                    {
                        notification.Status = NotificationStatus.Sent;
                        notification.SentAtUtc = DateTime.UtcNow;
                    }
                    notification.UpdatedAtUtc = DateTime.UtcNow;

                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Task {TaskId} created successfully for farm {FarmId}", task.Id, farm.Id);

            return TaskDto.FromEntity(task);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create task for farm {FarmId}", request.FarmId);
            throw;
        }
    }
}
