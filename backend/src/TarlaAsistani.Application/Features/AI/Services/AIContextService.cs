using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.AI.Services;

/// <summary>
/// Builds the per-request AI account context from the authenticated user's
/// authoritative database records.
///
/// Design decisions:
/// - Always uses authenticated userId from JWT (never client-supplied).
/// - hintFarmId from mobile is validated against ownership before use.
/// - Weather is fetched only when the message is weather-relevant.
/// - Multiple farm weather: cache hits first, then bounded batch (≤ MaxWeatherFarmsPerRequest).
/// - On weather provider failure: stale snapshot fallback, never crashes the AI request.
/// </summary>
public class AIContextService : IAIContextService
{
    private readonly IApplicationDbContext _db;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly FarmWorkWeatherSignalEvaluator _workWeatherEvaluator;

    // Bounded: AI request won't trigger more than this many external weather calls
    private const int MaxWeatherFarmsPerRequest = 5;

    public AIContextService(
        IApplicationDbContext db,
        IWeatherProvider weatherProvider,
        IMemoryCache cache,
        IConfiguration config,
        FarmWorkWeatherSignalEvaluator workWeatherEvaluator)
    {
        _db = db;
        _weatherProvider = weatherProvider;
        _cache = cache;
        _config = config;
        _workWeatherEvaluator = workWeatherEvaluator;
    }

    public async Task<AIAccountContext> BuildContextAsync(
        Guid userId,
        string message,
        Guid? hintFarmId,
        CancellationToken cancellationToken = default)
    {
        // 1. Load user profile (display name)
        var profile = await _db.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var displayName = profile?.FullName;

        // 2. Load all active farms for this user (DB source of truth)
        var farms = await _db.Farms
            .AsNoTracking()
            .Where(f => f.OwnerId == userId && f.ArchivedAt == null)
            .ToListAsync(cancellationToken);

        if (farms.Count == 0)
        {
            return new AIAccountContext(displayName, new List<AIFarmSummary>());
        }

        // 3. Determine whether weather context is needed
        var isWeatherRelevant = WeatherIntentDetector.IsWeatherRelevant(message);

        // 4. Determine target farms (for weather + detail)
        //    Priority: hint → message name match → all farms (bounded)
        var farmIds = farms.Select(f => f.Id).ToHashSet();

        // Validate hintFarmId ownership (never trust client as source of truth)
        Guid? validatedHintFarmId = null;
        if (hintFarmId.HasValue && farmIds.Contains(hintFarmId.Value))
        {
            validatedHintFarmId = hintFarmId.Value;
        }

        // Try matching farm name from message
        var farmNamePairs = farms.Select(f => (f.Id, f.Name)).ToList();
        var matchedFarmId = WeatherIntentDetector.TryMatchFarmByName(message, farmNamePairs);

        // Determine which farms get weather (ordered by relevance)
        List<Guid> weatherFarmIds;
        if (validatedHintFarmId.HasValue)
        {
            // Specific farm from screen context
            weatherFarmIds = new List<Guid> { validatedHintFarmId.Value };
        }
        else if (matchedFarmId.HasValue)
        {
            // Farm named in message
            weatherFarmIds = new List<Guid> { matchedFarmId.Value };
        }
        else if (isWeatherRelevant)
        {
            // All farms (bounded), prefer ones with coordinates
            weatherFarmIds = farms
                .Where(f => f.Latitude.HasValue && f.Longitude.HasValue)
                .Take(MaxWeatherFarmsPerRequest)
                .Select(f => f.Id)
                .ToList();
        }
        else
        {
            weatherFarmIds = new List<Guid>();
        }

        // 5. Fetch weather for selected farms (cache + bounded external calls)
        var weatherContextMap = new Dictionary<Guid, WeatherContextBundle?>();
        if (isWeatherRelevant && weatherFarmIds.Count > 0)
        {
            await FetchWeatherForFarmsAsync(
                farms.Where(f => weatherFarmIds.Contains(f.Id)).ToList(),
                weatherContextMap,
                cancellationToken);
        }

        // 6. Load next task and last activity per farm (single query each)
        var farmTaskMap = await LoadNextTasksAsync(farmIds, cancellationToken);
        var farmActivityMap = await LoadLastActivitiesAsync(farmIds, cancellationToken);
        var farmCropMap = await LoadActiveCropsAsync(farmIds, cancellationToken);

        // 7. Build compact summaries
        var summaries = farms.Select(farm =>
        {
            farmTaskMap.TryGetValue(farm.Id, out var nextTask);
            farmActivityMap.TryGetValue(farm.Id, out var lastActivity);
            farmCropMap.TryGetValue(farm.Id, out var crop);
            weatherContextMap.TryGetValue(farm.Id, out var weatherBundle);
            var weatherRequested = isWeatherRelevant && weatherFarmIds.Contains(farm.Id);
            var workWeatherSignal = nextTask != null && weatherRequested
                ? _workWeatherEvaluator.Evaluate(new FarmWorkWeatherEvaluationInput(
                    FarmId: farm.Id,
                    TaskId: nextTask.Id,
                    TaskTitle: nextTask.Title,
                    TaskDescription: nextTask.Description,
                    TaskDueDate: nextTask.DueDate,
                    ForecastPoints: weatherBundle?.ForecastPoints,
                    WeatherAvailable: weatherBundle != null,
                    IsStaleWeather: weatherBundle?.PromptContext.IsStale ?? false,
                    EvaluatedAtUtc: DateTime.UtcNow))
                : null;

            return new AIFarmSummary(
                FarmId: farm.Id,
                Name: farm.Name,
                CurrentCrop: crop,
                AreaHa: farm.SizeInHectares,
                NextTask: nextTask?.Title,
                NextTaskDueDate: nextTask?.DueDate,
                LastActivity: lastActivity?.Description,
                LastActivityAt: lastActivity?.OccurredAtUtc,
                Weather: weatherBundle?.PromptContext,
                WeatherRequested: weatherRequested,
                WorkWeatherSignal: workWeatherSignal
            );
        }).ToList();

        var activeAdvisories = await _db.ProactiveAdvisories
            .Include(a => a.Farm)
            .AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDismissed && (a.ValidUntilUtc == null || a.ValidUntilUtc > DateTime.UtcNow))
            .OrderByDescending(a => a.Severity)
            .Take(5)
            .Select(a => new ProactiveAdvisoryDto(
                a.Id,
                a.FarmId,
                a.Farm != null ? a.Farm.Name : string.Empty,
                a.UserId,
                a.RelatedTaskId,
                a.AdvisoryType,
                a.Severity,
                a.ActionType,
                a.Title,
                a.Summary,
                a.AgronomicExplanation,
                a.ActionRecommendation,
                a.RecommendedDate,
                a.MetricsJson,
                a.IsApplied,
                a.IsDismissed,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AIAccountContext(displayName, summaries, activeAdvisories);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task FetchWeatherForFarmsAsync(
        List<TarlaAsistani.Domain.Entities.Farm> farms,
        Dictionary<Guid, WeatherContextBundle?> resultMap,
        CancellationToken cancellationToken)
    {
        var cacheMinutes = _config.GetValue("Weather:CacheMinutes", 10);
        var staleAfterHours = _config.GetValue("Weather:StaleAfterHours", 4);
        var now = DateTime.UtcNow;

        var cacheMisses = new List<TarlaAsistani.Domain.Entities.Farm>();
        foreach (var farm in farms)
        {
            if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
            {
                resultMap[farm.Id] = null;
                continue;
            }

            var cacheKey = $"weather:{farm.Id}";

            // Cache hit — use cached FarmWeatherResponseDto
            if (_cache.TryGetValue(cacheKey, out FarmWeatherResponseDto? cached) && cached != null)
            {
                resultMap[farm.Id] = BuildWeatherContextBundle(farm.Name, cached);
                continue;
            }

            cacheMisses.Add(farm);
        }

        if (cacheMisses.Count == 0)
            return;

        try
        {
            var weatherData = await _weatherProvider.GetWeatherBatchAsync(
                cacheMisses
                    .Select(farm => (farm.Latitude!.Value, farm.Longitude!.Value))
                    .ToList(),
                cancellationToken);

            for (var index = 0; index < cacheMisses.Count; index++)
            {
                var farm = cacheMisses[index];
                var forecast = index < weatherData.Count ? weatherData[index] : null;
                if (forecast?.Points == null || forecast.Points.Count == 0)
                {
                    resultMap[farm.Id] = await TryLoadStaleWeatherAsync(
                        farm.Id, farm.Name, staleAfterHours, now, cancellationToken);
                    continue;
                }

                var dto = new FarmWeatherResponseDto(
                    FarmId: farm.Id,
                    Provider: _weatherProvider.Name,
                    FetchedAt: now,
                    IsStale: false,
                    StaleReason: null,
                    Points: forecast.Points,
                    Risks: WeatherRiskEvaluator.Evaluate(forecast.Points, now),
                    Current: forecast.Current,
                    Daily: forecast.Daily
                );

                _cache.Set($"weather:{farm.Id}", dto, TimeSpan.FromMinutes(cacheMinutes));
                resultMap[farm.Id] = BuildWeatherContextBundle(farm.Name, dto);
            }
        }
        catch
        {
            // A failed batch must not fail the AI response. Use each farm's stale
            // snapshot without issuing unbounded per-farm retry requests.
            foreach (var farm in cacheMisses)
            {
                resultMap[farm.Id] = await TryLoadStaleWeatherAsync(
                    farm.Id, farm.Name, staleAfterHours, now, cancellationToken);
            }
        }
    }

    private async Task<WeatherContextBundle?> TryLoadStaleWeatherAsync(
        Guid farmId,
        string farmName,
        int staleAfterHours,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var latestSnapshot = await _db.WeatherSnapshots
                .AsNoTracking()
                .Where(s => s.FarmId == farmId)
                .OrderByDescending(s => s.FetchedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSnapshot == null) return null;

            var points = JsonSerializer.Deserialize<List<TarlaAsistani.Application.Common.Interfaces.WeatherPoint>>(
                latestSnapshot.Payload);
            if (points == null || points.Count == 0) return null;

            var risks = WeatherRiskEvaluator.Evaluate(points, now);
            var firstPoint = points.FirstOrDefault();
            var dto = new FarmWeatherResponseDto(
                FarmId: farmId,
                Provider: latestSnapshot.Provider,
                FetchedAt: latestSnapshot.FetchedAtUtc,
                IsStale: true,
                StaleReason: "Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor.",
                Points: points,
                Risks: risks,
                Current: firstPoint != null ? new TarlaAsistani.Application.Features.Weather.DTOs.CurrentWeatherDto(
                    ObservedAt: firstPoint.ObservedAt,
                    TemperatureC: firstPoint.TemperatureC,
                    FeelsLikeC: firstPoint.TemperatureC,
                    HumidityPercent: firstPoint.HumidityPercent,
                    WindSpeedKmh: firstPoint.WindSpeedKmh,
                    WindGustsKmh: null,
                    Condition: null,
                    WeatherCode: firstPoint.WeatherCode) : null,
                Daily: null
            );

            return BuildWeatherContextBundle(farmName, dto);
        }
        catch
        {
            return null;
        }
    }

    private static AIWeatherAiContext BuildAIWeatherContext(string farmName, FarmWeatherResponseDto dto)
    {
        var ctx = dto.ToAiContext();
        var riskSummaries = ctx.ActiveRisks
            .Select(r => $"{r.RiskType}: {r.Message}")
            .ToList();

        return new AIWeatherAiContext(
            FarmName: farmName,
            CurrentTemperatureC: ctx.CurrentTemperatureC,
            HumidityPercent: ctx.CurrentHumidityPercent,
            WindSpeedKmh: ctx.CurrentWindSpeedKmh,
            Condition: ctx.Condition,
            NextRainProbabilityPct: ctx.NextRainProbability,
            Next24HoursPrecipitationMm: ctx.Next24HoursPrecipitationMm,
            IsStale: ctx.IsStale,
            StaleReason: ctx.StaleReason,
            DataTime: ctx.FetchedAtUtc,
            RiskSummaries: riskSummaries
        );
    }

    private static WeatherContextBundle BuildWeatherContextBundle(string farmName, FarmWeatherResponseDto dto) =>
        new(BuildAIWeatherContext(farmName, dto), dto.Points);

    private sealed record WeatherContextBundle(
        AIWeatherAiContext PromptContext,
        IReadOnlyList<WeatherPoint> ForecastPoints);

    private async Task<Dictionary<Guid, TarlaAsistani.Domain.Entities.FarmTask?>> LoadNextTasksAsync(
        HashSet<Guid> farmIds,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasks = await _db.FarmTasks
            .AsNoTracking()
            .Where(t => farmIds.Contains(t.FarmId) &&
                        t.Status != TaskStatus.Completed &&
                        t.Status != TaskStatus.Cancelled &&
                        t.Status != TaskStatus.NotApplied)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        // Per farm: pick soonest upcoming task
        return tasks
            .GroupBy(t => t.FarmId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.DueDate).FirstOrDefault()
            );
    }

    private async Task<Dictionary<Guid, TarlaAsistani.Domain.Entities.Activity?>> LoadLastActivitiesAsync(
        HashSet<Guid> farmIds,
        CancellationToken cancellationToken)
    {
        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => farmIds.Contains(a.FarmId) && a.ArchivedAtUtc == null)
            .OrderByDescending(a => a.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return activities
            .GroupBy(a => a.FarmId)
            .ToDictionary(
                g => g.Key,
                g => (TarlaAsistani.Domain.Entities.Activity?)g.First()
            );
    }

    private async Task<Dictionary<Guid, string?>> LoadActiveCropsAsync(
        HashSet<Guid> farmIds,
        CancellationToken cancellationToken)
    {
        var crops = await _db.CropPeriods
            .AsNoTracking()
            .Where(cp => farmIds.Contains(cp.FarmId) &&
                         cp.Status == TarlaAsistani.Domain.Enums.CropPeriodStatus.Active)
            .OrderByDescending(cp => cp.PlantedAt)
            .ToListAsync(cancellationToken);

        return crops
            .GroupBy(cp => cp.FarmId)
            .ToDictionary(
                g => g.Key,
                g => !string.IsNullOrWhiteSpace(g.First().CropName)
                    ? g.First().CropName
                    : g.First().CropType?.ToString()
            );
    }
}
