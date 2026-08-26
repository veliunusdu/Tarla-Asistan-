using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Activities.Commands;

public class UpdateActivityCommandHandler : IRequestHandler<UpdateActivityCommand, ActivityDto?>
{
    private readonly IApplicationDbContext _db;

    public UpdateActivityCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ActivityDto?> Handle(UpdateActivityCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch owned activity (join with Farm to verify ownership and farm not archived)
        var activity = await _db.Activities
            .Include(a => a.Farm)
            .FirstOrDefaultAsync(a => a.Id == request.ActivityId && a.Farm.OwnerId == request.UserId && a.ArchivedAtUtc == null && a.Farm.ArchivedAt == null, cancellationToken);

        if (activity == null)
        {
            return null;
        }

        // 2. Validate crop period if updating
        if (request.CropPeriodId.HasValue)
        {
            var cropPeriodValid = await _db.CropPeriods
                .AnyAsync(cp => cp.Id == request.CropPeriodId.Value && cp.FarmId == activity.FarmId, cancellationToken);

            if (!cropPeriodValid)
            {
                throw new ArgumentException("Üretim dönemi bu tarlaya ait değil.");
            }
        }

        // 3. Track previous values for revision history
        var previousValues = new Dictionary<string, object?>();

        if (request.ActivityType.HasValue && request.ActivityType.Value != activity.ActivityType)
        {
            previousValues["activity_type"] = activity.ActivityType.ToString();
            activity.ActivityType = request.ActivityType.Value;
        }

        if (request.Description != null && request.Description.Trim() != activity.Description)
        {
            previousValues["description"] = activity.Description;
            activity.Description = request.Description.Trim();
        }

        if (request.OccurredAt.HasValue)
        {
            var occurredUtc = request.OccurredAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.OccurredAt.Value, DateTimeKind.Utc)
                : request.OccurredAt.Value.ToUniversalTime();

            if (occurredUtc != activity.OccurredAtUtc)
            {
                previousValues["occurred_at"] = activity.OccurredAtUtc.ToString("o");
                activity.OccurredAtUtc = occurredUtc;
            }
        }

        if (request.CropPeriodId.HasValue && request.CropPeriodId != activity.CropPeriodId)
        {
            previousValues["crop_period_id"] = activity.CropPeriodId?.ToString();
            activity.CropPeriodId = request.CropPeriodId.Value;
        }

        if (request.DurationMinutes.HasValue && request.DurationMinutes != activity.DurationMinutes)
        {
            previousValues["duration_minutes"] = activity.DurationMinutes;
            activity.DurationMinutes = request.DurationMinutes;
        }

        if (request.Amount.HasValue && request.Amount != activity.Amount)
        {
            previousValues["amount"] = activity.Amount;
            activity.Amount = request.Amount;
        }

        if (request.Unit != null && request.Unit.Trim() != activity.Unit)
        {
            previousValues["unit"] = activity.Unit;
            activity.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        }

        if (request.PhotoUrl != null && request.PhotoUrl.Trim() != activity.PhotoUrl)
        {
            previousValues["photo_url"] = activity.PhotoUrl;
            activity.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim();
        }

        if (request.VoiceUrl != null && request.VoiceUrl.Trim() != activity.VoiceUrl)
        {
            previousValues["voice_url"] = activity.VoiceUrl;
            activity.VoiceUrl = string.IsNullOrWhiteSpace(request.VoiceUrl) ? null : request.VoiceUrl.Trim();
        }

        if (request.VoiceTranscript != null && request.VoiceTranscript.Trim() != activity.VoiceTranscript)
        {
            previousValues["voice_transcript"] = activity.VoiceTranscript;
            activity.VoiceTranscript = string.IsNullOrWhiteSpace(request.VoiceTranscript) ? null : request.VoiceTranscript.Trim();
        }

        if (request.PerformedBy != null && request.PerformedBy.Trim() != activity.PerformedBy)
        {
            previousValues["performed_by"] = activity.PerformedBy;
            activity.PerformedBy = string.IsNullOrWhiteSpace(request.PerformedBy) ? null : request.PerformedBy.Trim();
        }

        if (request.Cost.HasValue && request.Cost != activity.Cost)
        {
            previousValues["cost"] = activity.Cost;
            activity.Cost = request.Cost;
        }

        // If there were any changes, write revision history
        if (previousValues.Count > 0)
        {
            var now = DateTime.UtcNow;
            activity.UpdatedAtUtc = now;

            var revision = new ActivityRevision
            {
                ActivityId = activity.Id,
                ChangedById = request.UserId,
                PreviousValues = JsonSerializer.Serialize(previousValues),
                ChangedAtUtc = now
            };

            _db.ActivityRevisions.Add(revision);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ActivityDto.FromEntity(activity);
    }
}
