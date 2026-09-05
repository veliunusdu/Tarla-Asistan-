using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Services;

public record TaskSpec(
    string Title,
    string Description,
    string Reason,
    TaskPriority Priority,
    TaskSource Source,
    TaskConfidence Confidence,
    DateOnly DueDate,
    Guid? CropPeriodId = null,
    string DedupeDiscriminator = ""
);

public static class TaskEngine
{
    public static async Task EnsureDailyTasksAsync(
        IApplicationDbContext db,
        Farm farm,
        DateOnly targetDate,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (targetDate != today || farm.ArchivedAt != null)
        {
            return;
        }

        var cropPeriod = await db.CropPeriods
            .FirstOrDefaultAsync(cp => cp.FarmId == farm.Id && cp.Status == CropPeriodStatus.Active, cancellationToken);

        var specs = new List<TaskSpec>();

        // 1. Crop calendar growth check
        if (cropPeriod != null)
        {
            var growingDay = Math.Max(targetDate.DayNumber - cropPeriod.PlantedAt.DayNumber + 1, 1);
            var cropDisplayName = !string.IsNullOrWhiteSpace(cropPeriod.CropName)
                ? cropPeriod.CropName
                : cropPeriod.CropType?.ToString();
            specs.Add(new TaskSpec(
                Title: "Ürün gelişimini sahada kontrol edin",
                Description: "Bitki gelişimini, toprak nemini ve olağan dışı belirtileri kontrol edip gözlemlerinizi tarla günlüğüne kaydedin.",
                Reason: $"{cropDisplayName} üretim döneminin {growingDay}. günü için düzenli saha kontrolü.",
                Priority: TaskPriority.Medium,
                Source: TaskSource.CropCalendar,
                Confidence: TaskConfidence.Medium,
                DueDate: targetDate,
                CropPeriodId: cropPeriod.Id,
                DedupeDiscriminator: "daily-field-check"
            ));
        }

        // 2. Weekly activity reminder
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var recentActivityExists = await db.Activities
            .AnyAsync(a => a.FarmId == farm.Id &&
                           a.Status == ActivityStatus.Confirmed &&
                           a.ArchivedAtUtc == null &&
                           a.OccurredAtUtc >= sevenDaysAgo, cancellationToken);

        if (!recentActivityExists)
        {
            specs.Add(new TaskSpec(
                Title: "Tarla günlüğünü güncelleyin",
                Description: "Son sulama, gübreleme, ilaçlama veya saha kontrolü gibi işlemleri tarla günlüğüne ekleyin.",
                Reason: "Son yedi gün içinde doğrulanmış faaliyet kaydı bulunamadı.",
                Priority: TaskPriority.Low,
                Source: TaskSource.System,
                Confidence: TaskConfidence.High,
                DueDate: targetDate,
                CropPeriodId: cropPeriod?.Id,
                DedupeDiscriminator: "weekly-activity-reminder"
            ));
        }

        // 3. Weather risk tasks from fresh weather snapshot
        var latestSnapshot = await db.WeatherSnapshots
            .Where(s => s.FarmId == farm.Id)
            .OrderByDescending(s => s.FetchedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSnapshot != null && latestSnapshot.FetchedAtUtc >= DateTime.UtcNow.AddHours(-4))
        {
            try
            {
                var points = JsonSerializer.Deserialize<List<WeatherPoint>>(latestSnapshot.Payload);
                if (points != null && points.Count > 0)
                {
                    var risks = WeatherRiskEvaluator.Evaluate(points, DateTime.UtcNow);
                    foreach (var risk in risks)
                    {
                        var title = risk.RiskType switch
                        {
                            "FROST" => "Don riskine karşı tarlanızı kontrol edin",
                            "STRONG_WIND" => "Kuvvetli rüzgâr riskini değerlendirin",
                            "HEAVY_RAIN" => "Yoğun yağış riskini değerlendirin",
                            _ => "Hava koşullarını değerlendirin"
                        };

                        specs.Add(new TaskSpec(
                            Title: title,
                            Description: risk.SuggestedAction,
                            Reason: $"{risk.Message} Hava verisi {latestSnapshot.Provider} tarafından güncellendi.",
                            Priority: risk.Severity == "CRITICAL" ? TaskPriority.Critical : TaskPriority.High,
                            Source: TaskSource.Weather,
                            Confidence: TaskConfidence.Medium,
                            DueDate: targetDate,
                            CropPeriodId: cropPeriod?.Id,
                            DedupeDiscriminator: risk.RiskType
                        ));
                    }
                }
            }
            catch
            {
                // Ignore payload parsing errors in background engine
            }
        }

        // 4. Create tasks with deduplication
        var distinctSpecs = specs
            .GroupBy(s => CalculateDedupeKey(s))
            .Select(g => g.First())
            .ToList();

        var activeOrOverdueStatuses = new[]
        {
            TaskStatus.New,
            TaskStatus.Viewed,
            TaskStatus.Planned,
            TaskStatus.Overdue
        };

        var now = DateTime.UtcNow;
        foreach (var spec in distinctSpecs)
        {
            var dedupeKey = CalculateDedupeKey(spec);
            var legacyDedupeKey = CalculateLegacyDedupeKey(spec);

            // Prevent duplicate creation:
            // 1. If task with same dedupeKey (new or legacy) already exists for targetDate (any status)
            // 2. OR if an active/overdue task with same dedupeKey (new or legacy) already exists for this farm
            var alreadyExists = await db.FarmTasks
                .AnyAsync(t => t.FarmId == farm.Id &&
                               (t.DedupeKey == dedupeKey || t.DedupeKey == legacyDedupeKey) &&
                               (t.DueDate == targetDate || activeOrOverdueStatuses.Contains(t.Status)),
                          cancellationToken);

            if (!alreadyExists)
            {
                db.FarmTasks.Add(new FarmTask
                {
                    Id = Guid.NewGuid(),
                    FarmId = farm.Id,
                    Title = spec.Title,
                    Description = spec.Description,
                    Reason = spec.Reason,
                    Priority = spec.Priority,
                    Source = spec.Source,
                    Confidence = spec.Confidence,
                    Status = TaskStatus.New,
                    DueDate = spec.DueDate,
                    CropPeriodId = spec.CropPeriodId,
                    DedupeKey = dedupeKey,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static string CalculateDedupeKey(TaskSpec spec)
    {
        var discriminator = string.IsNullOrWhiteSpace(spec.DedupeDiscriminator)
            ? spec.Title.Trim().ToLowerInvariant()
            : spec.DedupeDiscriminator.Trim().ToLowerInvariant();
        var raw = $"{spec.Source}|{discriminator}|{spec.CropPeriodId}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public static string CalculateLegacyDedupeKey(TaskSpec spec)
    {
        var raw = $"{spec.Source}|{spec.Title.ToLowerInvariant()}|{spec.Description.ToLowerInvariant()}|{spec.Reason.ToLowerInvariant()}|{spec.CropPeriodId}|{spec.DedupeDiscriminator}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
