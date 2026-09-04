using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Activities.Queries;

public class GetFarmJournalQueryHandler : IRequestHandler<GetFarmJournalQuery, FarmJournalResponseDto?>
{
    private readonly IApplicationDbContext _db;

    public GetFarmJournalQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FarmJournalResponseDto?> Handle(GetFarmJournalQuery request, CancellationToken cancellationToken)
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

        // 2. Query Activities
        var activities = await _db.Activities
            .Where(a => a.FarmId == request.FarmId &&
                        a.Status == ActivityStatus.Confirmed &&
                        a.ArchivedAtUtc == null &&
                        a.Source != ActivitySource.Task)
            .ToListAsync(cancellationToken);

        // 3. Query Terminal Tasks
        var terminalStatuses = new List<TaskStatus> { TaskStatus.Completed, TaskStatus.NotApplied, TaskStatus.Cancelled };
        var tasks = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId && terminalStatuses.Contains(t.Status))
            .ToListAsync(cancellationToken);

        // 4. Transform into JournalEntryDto list
        var entries = new List<JournalEntryDto>();

        foreach (var a in activities)
        {
            var meta = new Dictionary<string, string?>
            {
                ["source"] = a.Source.ToString(),
                ["photo_url"] = a.PhotoUrl,
                ["voice_url"] = a.VoiceUrl
            };

            var title = !string.IsNullOrWhiteSpace(a.ActivityName)
                ? a.ActivityName
                : (a.ActivityType?.ToString() switch
                {
                    "Irrigation" => "Sulama",
                    "Fertilization" => "Gübreleme",
                    "Spraying" => "İlaçlama",
                    "Pruning" => "Budama",
                    "FieldCheck" => "Tarla Kontrolü",
                    "Harvest" => "Hasat",
                    "Other" => "Diğer",
                    _ => a.ActivityType?.ToString() ?? "Faaliyet"
                });

            entries.Add(new JournalEntryDto(
                EntryType: "ACTIVITY",
                Id: a.Id,
                OccurredAt: a.OccurredAtUtc,
                Title: title,
                Description: a.Description,
                Metadata: meta
            ));
        }

        foreach (var t in tasks)
        {
            var desc = t.Status == TaskStatus.NotApplied
                ? t.NotAppliedReason
                : (t.CompletionNote ?? t.Description);

            var meta = new Dictionary<string, string?>
            {
                ["status"] = t.Status.ToString(),
                ["source"] = t.Source.ToString(),
                ["priority"] = t.Priority.ToString(),
                ["photo_url"] = t.PhotoUrl
            };

            entries.Add(new JournalEntryDto(
                EntryType: "TASK",
                Id: t.Id,
                OccurredAt: t.CompletedAtUtc ?? t.UpdatedAtUtc,
                Title: t.Title,
                Description: desc,
                Metadata: meta
            ));
        }

        // 5. Order chronologically descending
        var sortedEntries = entries.OrderByDescending(e => e.OccurredAt).ToList();
        var total = sortedEntries.Count;
        var limit = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(request.Offset, 0);

        var paged = sortedEntries
            .Skip(offset)
            .Take(limit)
            .ToList();

        return new FarmJournalResponseDto(paged, total, limit, offset);
    }
}
