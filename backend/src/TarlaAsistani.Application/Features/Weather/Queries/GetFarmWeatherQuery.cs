using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Weather.Queries;

public record GetFarmWeatherQuery(
    Guid FarmId,
    Guid UserId
) : IRequest<FarmWeatherResponseDto>;

public class GetFarmWeatherQueryHandler : IRequestHandler<GetFarmWeatherQuery, FarmWeatherResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IConfiguration _config;

    public GetFarmWeatherQueryHandler(
        IApplicationDbContext db,
        IWeatherProvider weatherProvider,
        IConfiguration config)
    {
        _db = db;
        _weatherProvider = weatherProvider;
        _config = config;
    }

    public async Task<FarmWeatherResponseDto> Handle(GetFarmWeatherQuery request, CancellationToken cancellationToken)
    {
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
        {
            throw new ArgumentException("Hava durumu için tarlanın konumu tanımlanmalıdır.");
        }

        if (farm.Latitude.Value < -90.0 || farm.Latitude.Value > 90.0 ||
            farm.Longitude.Value < -180.0 || farm.Longitude.Value > 180.0)
        {
            throw new ArgumentException("Geçersiz tarla koordinatları.");
        }

        var now = DateTime.UtcNow;
        var staleAfterHours = _config.GetValue("Weather:StaleAfterHours", 4);
        var isStale = false;
        string? staleReason = null;
        List<WeatherPoint> points;
        DateTime fetchedAt;
        string providerName = _weatherProvider.Name;

        try
        {
            points = await _weatherProvider.ForecastAsync(farm.Latitude.Value, farm.Longitude.Value, cancellationToken);
            if (points == null || points.Count == 0)
            {
                throw new InvalidOperationException("Hava durumu sağlayıcısı boş veri döndürdü.");
            }

            var snapshot = new WeatherSnapshot
            {
                FarmId = farm.Id,
                Provider = _weatherProvider.Name,
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

        return new FarmWeatherResponseDto(
            FarmId: farm.Id,
            Provider: providerName,
            FetchedAt: fetchedAt,
            IsStale: isStale,
            StaleReason: staleReason,
            Points: points,
            Risks: risks
        );
    }
}
