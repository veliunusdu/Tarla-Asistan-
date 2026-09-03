using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Weather.Queries;

public record GetFarmWeatherQuery(
    Guid FarmId,
    Guid UserId,
    UserRole? Role = null
) : IRequest<FarmWeatherResponseDto>;

public class GetFarmWeatherQueryHandler : IRequestHandler<GetFarmWeatherQuery, FarmWeatherResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    public GetFarmWeatherQueryHandler(
        IApplicationDbContext db,
        IWeatherProvider weatherProvider,
        IConfiguration config,
        IMemoryCache cache)
    {
        _db = db;
        _weatherProvider = weatherProvider;
        _config = config;
        _cache = cache;
    }

    public async Task<FarmWeatherResponseDto> Handle(GetFarmWeatherQuery request, CancellationToken cancellationToken)
    {
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        var isOwner = farm.OwnerId == request.UserId;
        var isAgronomist = request.Role == UserRole.Agronomist;

        if (!isOwner && !isAgronomist)
        {
            throw new KeyNotFoundException("Tarla bulunamadı.");
        }

        if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
        {
            throw new ArgumentException("Hava durumu için tarlanın konumu tanımlanmalıdır.");
        }

        if (farm.Latitude.Value < -90.0 || farm.Latitude.Value > 90.0 ||
            farm.Longitude.Value < -180.0 || farm.Longitude.Value > 180.0)
        {
            throw new ArgumentException("Geçersiz tarla koordinatları.");
        }

        // A location change must not reuse a forecast cached for the old coordinates.
        var cacheKey = $"weather:{farm.Id}:{farm.Latitude.Value:F6}:{farm.Longitude.Value:F6}";
        if (_cache.TryGetValue(cacheKey, out FarmWeatherResponseDto? cached) && cached != null)
        {
            return cached;
        }

        var now = DateTime.UtcNow;
        var staleAfterHours = _config.GetValue("Weather:StaleAfterHours", 4);
        var cacheMinutes = _config.GetValue("Weather:CacheMinutes", 10);
        var isStale = false;
        string? staleReason = null;
        List<WeatherPoint> points;
        CurrentWeatherDto? current = null;
        List<DailyForecastDto>? daily = null;
        DateTime fetchedAt;
        string providerName = !string.IsNullOrWhiteSpace(_weatherProvider.Name) ? _weatherProvider.Name : "open_meteo";

        try
        {
            WeatherForecastData? weatherData = null;
            var providerRequestFailed = false;
            try
            {
                weatherData = await _weatherProvider.GetWeatherAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
            }
            catch
            {
                providerRequestFailed = true;
            }

            if (!providerRequestFailed && (weatherData == null || weatherData.Points == null || weatherData.Points.Count == 0))
            {
                var fallbackPoints = await _weatherProvider.ForecastAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
                if (fallbackPoints != null && fallbackPoints.Count > 0)
                {
                    var firstPoint = fallbackPoints.FirstOrDefault();
                    var fallbackCurrent = firstPoint != null
                        ? new CurrentWeatherDto(
                            ObservedAt: firstPoint.ObservedAt,
                            TemperatureC: firstPoint.TemperatureC,
                            FeelsLikeC: firstPoint.TemperatureC,
                            HumidityPercent: firstPoint.HumidityPercent,
                            WindSpeedKmh: firstPoint.WindSpeedKmh,
                            WindGustsKmh: null,
                            Condition: null,
                            WeatherCode: firstPoint.WeatherCode)
                        : null;
                    weatherData = new WeatherForecastData(fallbackPoints, fallbackCurrent, null);
                }
            }

            if (weatherData == null || weatherData.Points == null || weatherData.Points.Count == 0)
            {
                throw new InvalidOperationException("Hava durumu sağlayıcısı boş veri döndürdü.");
            }

            points = weatherData.Points;
            current = weatherData.Current;
            daily = weatherData.Daily;

            var snapshot = new WeatherSnapshot
            {
                FarmId = farm.Id,
                Provider = providerName,
                Payload = JsonSerializer.Serialize(points),
                FetchedAtUtc = now
            };

            _db.WeatherSnapshots.Add(snapshot);
            await _db.SaveChangesAsync(cancellationToken);

            fetchedAt = now;
        }
        catch (Exception)
        {
            // Fallback to latest database snapshot
            var latestSnapshot = await _db.WeatherSnapshots
                .Where(s => s.FarmId == farm.Id)
                .OrderByDescending(s => s.FetchedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSnapshot == null)
            {
                throw new InvalidOperationException("Hava durumu şu anda alınamıyor. Daha sonra tekrar deneyin; bu sırada saha koşullarını yerinde kontrol edin.");
            }

            try
            {
                points = JsonSerializer.Deserialize<List<WeatherPoint>>(latestSnapshot.Payload)
                    ?? throw new InvalidOperationException("Kayıtlı hava durumu verisi okunamadı.");
            }
            catch
            {
                throw new InvalidOperationException("Hava durumu verisi şu anda kullanılamıyor.");
            }

            fetchedAt = latestSnapshot.FetchedAtUtc;
            providerName = latestSnapshot.Provider;
            isStale = true;
            staleReason = "Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor.";
        }

        if (fetchedAt < now.AddHours(-staleAfterHours))
        {
            isStale = true;
            staleReason ??= "Hava durumu verisi güncellik süresini aştı; saha koşullarını kontrol edin.";
        }

        var risks = WeatherRiskEvaluator.Evaluate(points, now);

        var result = new FarmWeatherResponseDto(
            FarmId: farm.Id,
            Provider: providerName,
            FetchedAt: fetchedAt,
            IsStale: isStale,
            StaleReason: staleReason,
            Points: points,
            Risks: risks,
            Current: current,
            Daily: daily
        );

        var ttl = isStale ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(cacheMinutes);
        _cache.Set(cacheKey, result, ttl);

        return result;
    }
}
