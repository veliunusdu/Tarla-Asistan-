using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, ActivityDto>
{
    private readonly IApplicationDbContext _db;

    public CreateActivityCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityDto> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm existence and ownership
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        // 2. Check Idempotency if ClientOperationId is provided
        if (request.ClientOperationId.HasValue)
        {
            var existingActivity = await _db.Activities
                .FirstOrDefaultAsync(a => a.ClientOperationId == request.ClientOperationId.Value && a.FarmId == request.FarmId, cancellationToken);

            if (existingActivity != null)
            {
                return ActivityDto.FromEntity(existingActivity);
            }
        }

        // 3. Validate CropPeriod if specified
        if (request.CropPeriodId.HasValue)
        {
            var cropPeriodExists = await _db.CropPeriods
                .AnyAsync(cp => cp.Id == request.CropPeriodId.Value && cp.FarmId == request.FarmId, cancellationToken);

            if (!cropPeriodExists)
            {
                throw new ArgumentException("Üretim dönemi bu tarlaya ait değil.");
            }
        }

        // 4. Determine status based on input method
        var isVoiceDraft = request.InputMethod == ActivitySource.Voice;
        var now = DateTime.UtcNow;

        var activity = new Activity
        {
            FarmId = farm.Id,
            CropPeriodId = request.CropPeriodId,
            CreatedById = request.CreatedById,
            ActivityType = request.ActivityType,
            Status = isVoiceDraft ? ActivityStatus.Draft : ActivityStatus.Confirmed,
            Source = request.InputMethod,
            Description = request.Description.Trim(),
            OccurredAtUtc = request.OccurredAt.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(request.OccurredAt, DateTimeKind.Utc) 
                : request.OccurredAt.ToUniversalTime(),
            DurationMinutes = request.DurationMinutes,
            Amount = request.Amount,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim(),
            VoiceUrl = string.IsNullOrWhiteSpace(request.VoiceUrl) ? null : request.VoiceUrl.Trim(),
            VoiceTranscript = string.IsNullOrWhiteSpace(request.VoiceTranscript) ? null : request.VoiceTranscript.Trim(),
            PerformedBy = string.IsNullOrWhiteSpace(request.PerformedBy) ? null : request.PerformedBy.Trim(),
            Cost = request.Cost,
            ConfirmedAtUtc = isVoiceDraft ? null : now,
            ClientOperationId = request.ClientOperationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.ClientOperationId.HasValue)
        {
            var payloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{farm.Id}:{request.ActivityType}:{request.Description.Trim()}"))).ToLowerInvariant();
            _db.ClientOperations.Add(new ClientOperation
            {
                ActorId = request.CreatedById,
                ClientOperationId = request.ClientOperationId.Value,
                Scope = $"activity.create:{farm.Id}",
                PayloadHash = payloadDigest,
                ResourceType = "activity",
                ResourceId = activity.Id,
                CreatedAtUtc = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ActivityDto.FromEntity(activity);
    }
}
