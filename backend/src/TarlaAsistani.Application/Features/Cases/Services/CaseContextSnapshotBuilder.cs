using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Services;

public sealed class CaseContextSnapshotBuilder
{
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration? _configuration;
    private readonly IMemoryCache? _cache;

    public CaseContextSnapshotBuilder(IApplicationDbContext db, IConfiguration? configuration = null, IMemoryCache? cache = null)
    {
        _db = db;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<CaseContextSnapshot> BuildAsync(Farm farm, Guid caseId, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        var crop = await _db.CropPeriods
            .AsNoTracking()
            .Where(cp => cp.FarmId == farm.Id && cp.Status == CropPeriodStatus.Active)
            .OrderByDescending(cp => cp.PlantedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.FarmId == farm.Id && a.ArchivedAtUtc == null)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(5)
            .Select(a => new RecentActivitySnapshotDto(
                a.Id, a.ActivityName, a.ActivityType.HasValue ? a.ActivityType.Value.ToString() : null,
                a.Status.ToString(), a.OccurredAtUtc, a.Description))
            .ToListAsync(cancellationToken);

        var cacheKey = farm.Latitude.HasValue && farm.Longitude.HasValue
            ? $"weather:{farm.Id}:{farm.Latitude.Value:F6}:{farm.Longitude.Value:F6}"
            : null;
        var cachedWeather = cacheKey == null ? null : _cache?.Get<FarmWeatherResponseDto>(cacheKey);
        var weather = cachedWeather == null
            ? await _db.WeatherSnapshots
                .AsNoTracking()
                .Where(s => s.FarmId == farm.Id)
                .OrderByDescending(s => s.FetchedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var points = cachedWeather?.Points ?? DeserializePoints(weather?.Payload);
        var weatherFetchedAt = cachedWeather?.FetchedAt ?? weather?.FetchedAtUtc;
        var weatherProvider = cachedWeather?.Provider ?? weather?.Provider;
        var staleAfterHours = int.TryParse(_configuration?["Weather:StaleAfterHours"], out var configuredHours)
            ? configuredHours
            : 4;
        var isStale = weatherFetchedAt.HasValue && weatherFetchedAt.Value < capturedAtUtc.AddHours(-staleAfterHours);
        var next24 = points.Take(24).ToList();

        return new CaseContextSnapshot
        {
            CaseId = caseId,
            FarmName = farm.Name,
            Latitude = farm.Latitude,
            Longitude = farm.Longitude,
            SizeInHectares = farm.SizeInHectares,
            IrrigationMethod = farm.IrrigationMethod?.ToString(),
            SoilType = farm.SoilType,
            FarmNote = farm.Note,
            CropName = crop?.CropName,
            CropPlantedAt = crop?.PlantedAt,
            CropHarvestedAt = crop?.HarvestedAt,
            CropGrowingDay = crop == null ? null : Math.Max(0, capturedAtUtc.Date.Subtract(crop.PlantedAt.ToDateTime(TimeOnly.MinValue).Date).Days + 1),
            WeatherProvider = weatherProvider,
            WeatherFetchedAtUtc = weatherFetchedAt,
            IsBasedOnStaleWeather = isStale,
            CurrentTemperatureC = points.FirstOrDefault()?.TemperatureC,
            CurrentHumidityPercent = points.FirstOrDefault()?.HumidityPercent,
            Next24HoursPrecipitationMm = points.Count == 0 ? null : next24.Sum(p => p.PrecipitationMm ?? 0),
            RecentActivitiesJson = JsonSerializer.Serialize(activities)
        };
    }

    private static List<WeatherPoint> DeserializePoints(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return [];
        try { return JsonSerializer.Deserialize<List<WeatherPoint>>(payload) ?? []; }
        catch (JsonException) { return []; }
    }
}
