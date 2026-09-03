using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.AI.Services;

public class AIQuotaService : IAIQuotaService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AIQuotaService> _logger;
    private readonly int _dailyPhotoLimit;
    private readonly int _dailyTextLimit;

    public AIQuotaService(
        IApplicationDbContext db,
        IConfiguration config,
        ILogger<AIQuotaService> logger)
    {
        _db = db;
        _logger = logger;

        _dailyPhotoLimit = ReadConfigInt(
            5,
            config["AI:Quotas:DailyPhotoLimit"],
            config["AI_QUOTAS_DAILY_PHOTO_LIMIT"]);

        _dailyTextLimit = ReadConfigInt(
            100,
            config["AI:Quotas:DailyTextMessageLimit"],
            config["AI_QUOTAS_DAILY_TEXT_MESSAGE_LIMIT"]);
    }

    private static int ReadConfigInt(int defaultVal, params string?[] values)
    {
        foreach (var val in values)
        {
            if (int.TryParse(val, out var parsed) && parsed > 0)
            {
                return parsed;
            }
        }
        return defaultVal;
    }

    public async Task CheckQuotaAsync(Guid userId, bool hasPhoto, CancellationToken cancellationToken = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        if (hasPhoto)
        {
            var photosToday = await _db.AiUsageLogs
                .CountAsync(l => l.UserId == userId && l.HasPhoto && l.CreatedAtUtc >= todayUtc, cancellationToken);

            if (photosToday >= _dailyPhotoLimit)
            {
                _logger.LogWarning("User {UserId} exceeded daily photo quota ({Used}/{Limit})", userId, photosToday, _dailyPhotoLimit);
                throw new QuotaExceededException($"Günlük fotoğraf analizi kotanıza ({_dailyPhotoLimit}/{_dailyPhotoLimit}) ulaştınız. Kotanız 00:00 UTC'de yenilenecektir.");
            }
        }
        else
        {
            var textsToday = await _db.AiUsageLogs
                .CountAsync(l => l.UserId == userId && !l.HasPhoto && l.CreatedAtUtc >= todayUtc, cancellationToken);

            if (textsToday >= _dailyTextLimit)
            {
                _logger.LogWarning("User {UserId} exceeded daily text message quota ({Used}/{Limit})", userId, textsToday, _dailyTextLimit);
                throw new QuotaExceededException($"Günlük soru sorma kotanıza ({_dailyTextLimit}/{_dailyTextLimit}) ulaştınız. Kotanız 00:00 UTC'de yenilenecektir.");
            }
        }
    }

    public async Task<AIQuotaStatusDto> GetQuotaStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var photosUsedToday = await _db.AiUsageLogs
            .CountAsync(l => l.UserId == userId && l.HasPhoto && l.CreatedAtUtc >= todayUtc, cancellationToken);

        var textsUsedToday = await _db.AiUsageLogs
            .CountAsync(l => l.UserId == userId && !l.HasPhoto && l.CreatedAtUtc >= todayUtc, cancellationToken);

        var photosRemaining = Math.Max(0, _dailyPhotoLimit - photosUsedToday);
        var textsRemaining = Math.Max(0, _dailyTextLimit - textsUsedToday);
        var resetsAtUtc = todayUtc.AddDays(1);

        return new AIQuotaStatusDto(
            DailyPhotoLimit: _dailyPhotoLimit,
            PhotosUsedToday: photosUsedToday,
            PhotosRemainingToday: photosRemaining,
            DailyTextLimit: _dailyTextLimit,
            TextsUsedToday: textsUsedToday,
            TextsRemainingToday: textsRemaining,
            ResetsAtUtc: resetsAtUtc
        );
    }

    public async Task RecordUsageAsync(
        Guid userId,
        string provider,
        string model,
        bool hasPhoto,
        int promptTokens,
        int completionTokens,
        long durationMs,
        decimal estimatedCostUsd,
        CancellationToken cancellationToken = default)
    {
        var totalTokens = promptTokens + completionTokens;

        var log = new AiUsageLog
        {
            UserId = userId,
            Provider = provider,
            Model = model,
            HasPhoto = hasPhoto,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = estimatedCostUsd,
            DurationMs = durationMs,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AiUsageLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI Usage Recorded — User: {UserId}, Provider: {Provider}, Model: {Model}, Photo: {HasPhoto}, Tokens: {PromptTokens}+{CompletionTokens}={TotalTokens}, Cost: ${Cost:F6}, Duration: {DurationMs}ms",
            userId, provider, model, hasPhoto, promptTokens, completionTokens, totalTokens, estimatedCostUsd, durationMs);
    }
}
