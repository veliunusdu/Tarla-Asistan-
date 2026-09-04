using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

public class FallbackWeatherProvider : IWeatherProvider
{
    private readonly IReadOnlyList<IWeatherProvider> _providers;
    private readonly ILogger<FallbackWeatherProvider> _logger;
    private string _activeProviderName = "composite_weather";

    public FallbackWeatherProvider(
        IEnumerable<IWeatherProvider> providers,
        ILogger<FallbackWeatherProvider> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
        if (_providers.Count > 0)
        {
            _activeProviderName = _providers[0].Name;
        }
    }

    public string Name => _activeProviderName;

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.GetWeatherAsync(latitude, longitude, cancellationToken);
                if (result?.Points != null && result.Points.Count > 0)
                {
                    _activeProviderName = provider.Name;
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Weather provider {ProviderName} failed for ({Lat}, {Lon}). Trying next provider...", provider.Name, latitude, longitude);
            }
        }

        throw new InvalidOperationException("Tüm hava durumu sağlayıcıları yanıt vermedi.", lastException);
    }

    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.GetWeatherBatchAsync(coordinates, cancellationToken);
                if (result != null && result.Count == coordinates.Count && result.All(r => r.Points != null && r.Points.Count > 0))
                {
                    _activeProviderName = provider.Name;
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Batch weather provider {ProviderName} failed. Trying next provider...", provider.Name);
            }
        }

        throw new InvalidOperationException("Tüm hava durumu sağlayıcıları toplu istekte başarısız oldu.", lastException);
    }
}
