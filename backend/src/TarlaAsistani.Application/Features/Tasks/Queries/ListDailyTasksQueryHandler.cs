using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Services;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public class ListDailyTasksQueryHandler : IRequestHandler<ListDailyTasksQuery, DailyTaskListDto?>
{
    private static readonly TaskStatus[] ActiveStatuses = [TaskStatus.New, TaskStatus.Viewed, TaskStatus.Planned];
    private static readonly TaskStatus[] TerminalStatuses = [TaskStatus.Completed, TaskStatus.NotApplied, TaskStatus.Cancelled];
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IApplicationDbContext _db;
    private readonly ILogger<ListDailyTasksQueryHandler> _logger;
    private readonly IWeatherProvider? _weatherProvider;
    private readonly IProactiveAdvisoryEngine _advisoryEngine;
    private readonly FarmWorkWeatherSignalEvaluator _signalEvaluator;
    private readonly IConfiguration? _configuration;

    public ListDailyTasksQueryHandler(
        IApplicationDbContext db,
        ILogger<ListDailyTasksQueryHandler>? logger = null,
        IWeatherProvider? weatherProvider = null,
        IProactiveAdvisoryEngine? advisoryEngine = null,
        FarmWorkWeatherSignalEvaluator? signalEvaluator = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _logger = logger ?? NullLogger<ListDailyTasksQueryHandler>.Instance;
        _weatherProvider = weatherProvider;
        _advisoryEngine = advisoryEngine ?? new ProactiveAdvisoryEngine();
        _signalEvaluator = signalEvaluator ?? new FarmWorkWeatherSignalEvaluator(new WeatherActionRiskOptions());
        _configuration = configuration;
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

        // 4. Query active tasks for target date and upcoming window (up to 14 days)
        var candidateTasks = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId &&
                        t.DueDate >= request.TargetDate &&
                        t.DueDate <= request.TargetDate.AddDays(14) &&
                        ActiveStatuses.Contains(t.Status))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        // 5. Split critical weather alerts vs regular candidate tasks
        var criticalWeatherAlerts = candidateTasks
            .Where(t => t.Source == TaskSource.Weather && t.Priority == TaskPriority.Critical && t.DueDate == request.TargetDate)
            .OrderBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .ToList();

        var regularCandidates = candidateTasks
            .Where(t => !(t.Source == TaskSource.Weather && t.Priority == TaskPriority.Critical))
            .ToList();

        // 6. Deterministically rank regular tasks with TaskRankingService and take top 3
        var rankedTasks = TaskRankingService.RankTasks(regularCandidates, request.TargetDate);
        var visibleTasks = rankedTasks
            .Take(3)
            .ToList();

        // 7. Query overdue tasks
        var overdueList = await _db.FarmTasks
            .Where(t => t.FarmId == request.FarmId && t.Status == TaskStatus.Overdue)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        // 8. Resolve Weather and attach WeatherPostponeSuggestion to tasks
        var nowUtc = DateTime.UtcNow;
        var allTasksToReturn = visibleTasks
            .Concat(criticalWeatherAlerts)
            .Concat(overdueList)
            .DistinctBy(t => t.Id)
            .ToList();

        var taskIds = allTasksToReturn.Select(t => t.Id).ToList();
        var existingAdvisories = await _db.ProactiveAdvisories
            .Where(a => a.FarmId == request.FarmId && a.RelatedTaskId != null && taskIds.Contains(a.RelatedTaskId.Value))
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var advisoriesByTaskId = existingAdvisories
            .GroupBy(a => a.RelatedTaskId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var weather = await ResolveFarmWeatherAsync(farm, cancellationToken);

        var visibleDtos = new List<TaskDto>(visibleTasks.Count);
        foreach (var task in visibleTasks)
        {
            var suggestion = await BuildSuggestionForTaskAsync(task, farm, weather, advisoriesByTaskId, nowUtc, cancellationToken);
            visibleDtos.Add(TaskDto.FromEntity(task, suggestion));
        }

        var criticalDtos = new List<TaskDto>(criticalWeatherAlerts.Count);
        foreach (var task in criticalWeatherAlerts)
        {
            var suggestion = await BuildSuggestionForTaskAsync(task, farm, weather, advisoriesByTaskId, nowUtc, cancellationToken);
            criticalDtos.Add(TaskDto.FromEntity(task, suggestion));
        }

        var overdueDtos = new List<TaskDto>(overdueList.Count);
        foreach (var task in overdueList)
        {
            var suggestion = await BuildSuggestionForTaskAsync(task, farm, weather, advisoriesByTaskId, nowUtc, cancellationToken);
            overdueDtos.Add(TaskDto.FromEntity(task, suggestion));
        }

        _logger.LogDebug(
            "Daily tasks evaluated for farm {FarmId}: {CandidateCount} candidates, {SelectedCount} visible tasks, {CriticalAlertCount} critical weather alerts, {OverdueCount} overdue tasks",
            request.FarmId,
            candidateTasks.Count,
            visibleDtos.Count,
            criticalDtos.Count,
            overdueDtos.Count);

        return new DailyTaskListDto(
            Date: request.TargetDate,
            Items: visibleDtos,
            CriticalWeatherAlerts: criticalDtos,
            Overdue: overdueDtos,
            VisibleLimit: 3
        );
    }

    public sealed record WeatherResolution(
        List<WeatherPoint>? Points,
        DateTime? FetchedAtUtc,
        bool IsStale,
        string? StaleReason,
        bool IsAvailable);

    public async Task<WeatherResolution> ResolveFarmWeatherAsync(
        Farm farm,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var staleAfterHours = WeatherDefaults.GetStaleAfterHours(_configuration);

        List<WeatherPoint>? points = null;
        DateTime? fetchedAt = null;
        bool isStale = false;
        string? staleReason = null;
        bool isAvailable = false;
        bool providerFailed = false;

        if (farm.Latitude.HasValue && farm.Longitude.HasValue && _weatherProvider != null)
        {
            try
            {
                var weatherData = await _weatherProvider.GetWeatherAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
                if (weatherData?.Points != null && weatherData.Points.Count > 0)
                {
                    points = weatherData.Points;
                    fetchedAt = nowUtc;
                    isAvailable = true;
                }
                else
                {
                    var fallbackPoints = await _weatherProvider.ForecastAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
                    if (fallbackPoints != null && fallbackPoints.Count > 0)
                    {
                        points = fallbackPoints.ToList();
                        fetchedAt = nowUtc;
                        isAvailable = true;
                    }
                    else
                    {
                        providerFailed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                providerFailed = true;
                _logger.LogWarning(ex, "Failed to fetch live weather from provider for farm {FarmId}", farm.Id);
            }
        }

        if (points == null || points.Count == 0)
        {
            var latestSnapshot = await _db.WeatherSnapshots
                .Where(s => s.FarmId == farm.Id)
                .OrderByDescending(s => s.FetchedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSnapshot != null)
            {
                try
                {
                    points = JsonSerializer.Deserialize<List<WeatherPoint>>(latestSnapshot.Payload);
                    if (points != null && points.Count > 0)
                    {
                        fetchedAt = latestSnapshot.FetchedAtUtc;
                        isAvailable = true;
                        if (latestSnapshot.FetchedAtUtc < nowUtc.AddHours(-staleAfterHours))
                        {
                            isStale = true;
                            staleReason = "Hava durumu verisi güncellik süresini aştı; saha koşullarını kontrol edin.";
                        }
                        else if (providerFailed)
                        {
                            isStale = true;
                            staleReason = "Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize weather snapshot for farm {FarmId}", farm.Id);
                }
            }
        }

        if (points == null || points.Count == 0)
        {
            isAvailable = false;
            staleReason = "Güncel hava verisine erişilemediği için iş zamanlaması değerlendirilemiyor.";
        }

        return new WeatherResolution(points, fetchedAt, isStale, staleReason, isAvailable);
    }

    public async Task<WeatherPostponeSuggestionDto?> BuildSuggestionForTaskAsync(
        FarmTask task,
        Farm farm,
        WeatherResolution weather,
        Dictionary<Guid, ProactiveAdvisory> advisoriesByTaskId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        bool isTerminal = TerminalStatuses.Contains(task.Status);

        // 1. If an advisory already exists in DB for this task, reuse it
        if (advisoriesByTaskId.TryGetValue(task.Id, out var existingAdvisory))
        {
            DateOnly? recommendedDate = existingAdvisory.RecommendedDate;
            string? staleReason = null;
            bool canApply = false;

            var isExpired = existingAdvisory.ValidUntilUtc.HasValue && nowUtc >= existingAdvisory.ValidUntilUtc.Value;

            if (isExpired)
            {
                recommendedDate = null;
                staleReason = "Bu hava önerisinin geçerlilik süresi doldu. Güncel hava durumunu kontrol edin.";
                canApply = false;
            }
            else if (weather.IsStale)
            {
                recommendedDate = null; // Do not provide actionable alternative date if weather is stale
                staleReason = weather.StaleReason ?? "Hava durumu verisi güncellik süresini aştı; saha koşullarını kontrol edin.";
                canApply = false;
            }
            else if (!weather.IsAvailable)
            {
                recommendedDate = null;
                staleReason = weather.StaleReason ?? "Güncel hava verisine erişilemediği için iş zamanlaması değerlendirilemiyor.";
                canApply = false;
            }
            else
            {
                canApply = !existingAdvisory.IsApplied &&
                           !existingAdvisory.IsDismissed &&
                           !isTerminal &&
                           existingAdvisory.RecommendedDate.HasValue;
            }

            var isStale = weather.IsStale || isExpired;
            var reasonText = isExpired ? staleReason! : existingAdvisory.Summary;
            var reasons = new List<string> { reasonText };

            return new WeatherPostponeSuggestionDto(
                AdvisoryId: existingAdvisory.Id,
                RiskLevel: existingAdvisory.Severity == AdvisorySeverity.Critical ? "High" : "Medium",
                Reason: reasonText,
                Reasons: reasons,
                SuggestedAction: isExpired ? "CheckCurrentWeather" : existingAdvisory.ActionType.ToString(),
                RecommendedDate: recommendedDate,
                EvaluatedAtUtc: existingAdvisory.CreatedAtUtc,
                WeatherFetchedAtUtc: weather.FetchedAtUtc,
                IsWeatherStale: isStale,
                StaleReason: staleReason,
                CanApply: canApply
            );
        }

        // 2. If no advisory exists in DB, check if task is weather-sensitive
        var isSpraying = IsSprayingTask(task.Title, task.Description);
        var isFertilizing = IsFertilizerTask(task.Title, task.Description);

        if (!isSpraying && !isFertilizing)
        {
            return null; // Normal task without weather postponement rule
        }

        // 3. If weather is completely unavailable for this weather-sensitive task
        if (!weather.IsAvailable)
        {
            var unavailableReason = weather.StaleReason ?? "Güncel hava verisine erişilemediği için iş zamanlaması değerlendirilemiyor.";
            return new WeatherPostponeSuggestionDto(
                AdvisoryId: null,
                RiskLevel: null,
                Reason: unavailableReason,
                Reasons: [unavailableReason],
                SuggestedAction: "WeatherUnavailable",
                RecommendedDate: null,
                EvaluatedAtUtc: nowUtc,
                WeatherFetchedAtUtc: null,
                IsWeatherStale: false,
                StaleReason: unavailableReason,
                CanApply: false
            );
        }

        // 4. If weather is stale
        if (weather.IsStale)
        {
            var forecastData = new WeatherForecastData(weather.Points ?? []);
            var staleResults = _advisoryEngine.Evaluate(farm, [], [task], forecastData, nowUtc);
            var staleMatch = staleResults.FirstOrDefault(r => r.RelatedTaskId == task.Id);

            if (staleMatch != null)
            {
                var staleReasonText = weather.StaleReason ?? "Hava durumu verisi güncellik süresini aştı; saha koşullarını kontrol edin.";
                return new WeatherPostponeSuggestionDto(
                    AdvisoryId: null,
                    RiskLevel: staleMatch.Severity == AdvisorySeverity.Critical ? "High" : "Medium",
                    Reason: staleMatch.Summary,
                    Reasons: [staleMatch.Summary],
                    SuggestedAction: staleMatch.ActionType.ToString(),
                    RecommendedDate: null, // Critical: never offer recommended date on stale weather
                    EvaluatedAtUtc: nowUtc,
                    WeatherFetchedAtUtc: weather.FetchedAtUtc,
                    IsWeatherStale: true,
                    StaleReason: staleReasonText,
                    CanApply: false
                );
            }

            return null;
        }

        // 5. Fresh weather: Evaluate with rule engine
        var weatherData = new WeatherForecastData(weather.Points ?? []);
        var evaluationResults = _advisoryEngine.Evaluate(farm, [], [task], weatherData, nowUtc);
        var match = evaluationResults.FirstOrDefault(r => r.RelatedTaskId == task.Id);

        if (match == null)
        {
            return null; // No weather risk!
        }

        // Persist the evaluated advisory using DedupeKey so it has a real AdvisoryId that can be applied
        var dedupeKey = match.DedupeKey;
        var advisory = await _db.ProactiveAdvisories
            .FirstOrDefaultAsync(a => a.DedupeKey == dedupeKey, cancellationToken);

        if (advisory == null)
        {
            DateTime? validUntil = nowUtc.AddDays(4);
            if (match.ActionType == ProactiveActionType.PostponeTask)
            {
                var staleHours = WeatherDefaults.GetStaleAfterHours(_configuration);
                validUntil = WeatherDefaults.CalculateWeatherAdvisoryValidUntil(
                    weather.FetchedAtUtc,
                    validUntil,
                    staleHours,
                    nowUtc);
            }

            advisory = new ProactiveAdvisory
            {
                FarmId = farm.Id,
                UserId = farm.OwnerId,
                CropPeriodId = task.CropPeriodId,
                RelatedTaskId = task.Id,
                AdvisoryType = match.AdvisoryType,
                Severity = match.Severity,
                ActionType = match.ActionType,
                Title = match.Title,
                Summary = match.Summary,
                AgronomicExplanation = match.AgronomicExplanation,
                ActionRecommendation = match.ActionRecommendation,
                RecommendedDate = match.RecommendedDate,
                MetricsJson = match.MetricsJson,
                DedupeKey = match.DedupeKey,
                ValidUntilUtc = validUntil,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _db.ProactiveAdvisories.Add(advisory);
            await _db.SaveChangesAsync(cancellationToken);
        }

        advisoriesByTaskId[task.Id] = advisory;

        bool canApplyFresh = !advisory.IsApplied &&
                             !advisory.IsDismissed &&
                             !isTerminal &&
                             advisory.RecommendedDate.HasValue &&
                             (!advisory.ValidUntilUtc.HasValue || advisory.ValidUntilUtc.Value > nowUtc);

        var reasonsList = new List<string>();
        var signal = _signalEvaluator.Evaluate(new FarmWorkWeatherEvaluationInput(
            farm.Id,
            task.Id,
            task.Title,
            task.Description,
            task.DueDate,
            weather.Points,
            weather.IsAvailable,
            weather.IsStale,
            nowUtc));

        if (signal?.Reasons != null && signal.Reasons.Count > 0)
        {
            reasonsList.AddRange(signal.Reasons);
        }
        if (!reasonsList.Contains(match.Summary))
        {
            reasonsList.Add(match.Summary);
        }

        return new WeatherPostponeSuggestionDto(
            AdvisoryId: advisory.Id,
            RiskLevel: advisory.Severity == AdvisorySeverity.Critical ? "High" : "Medium",
            Reason: reasonsList.FirstOrDefault() ?? advisory.Summary,
            Reasons: reasonsList,
            SuggestedAction: advisory.ActionType.ToString(),
            RecommendedDate: advisory.RecommendedDate,
            EvaluatedAtUtc: advisory.CreatedAtUtc,
            WeatherFetchedAtUtc: weather.FetchedAtUtc,
            IsWeatherStale: false,
            StaleReason: null,
            CanApply: canApplyFresh
        );
    }

    private static bool IsSprayingTask(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLower(TurkishCulture);
        return text.Contains("ilaç") || text.Contains("ilac") || text.Contains("spray") || text.Contains("fungisit") || text.Contains("insektisit") || text.Contains("herbisit");
    }

    private static bool IsFertilizerTask(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLower(TurkishCulture);
        return text.Contains("gübre") || text.Contains("fertiliz") || text.Contains("üre") || text.Contains("azot");
    }
}
