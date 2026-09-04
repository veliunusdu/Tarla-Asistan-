using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

public class WeatherApiWeatherProvider : IWeatherProvider
{
    public string Name => "weather_api";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public WeatherApiWeatherProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _baseUrl = config["Weather:WeatherApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("WEATHER_API_BASE_URL")
            ?? "https://api.weatherapi.com/v1";

        _apiKey = config["Weather:WeatherApiKey"]
            ?? Environment.GetEnvironmentVariable("WEATHER_API_KEY");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("WeatherAPI anahtarı yapılandırılmamış.");
        }

        var latStr = latitude.ToString("F4", CultureInfo.InvariantCulture);
        var lonStr = longitude.ToString("F4", CultureInfo.InvariantCulture);
        var url = $"{_baseUrl.TrimEnd('/')}/forecast.json?key={Uri.EscapeDataString(_apiKey)}&q={latStr},{lonStr}&days=3&aqi=no&alerts=no&lang=tr";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("WeatherAPI servis istek sınırına ulaştı (429).");
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResponse(json);
    }

    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WeatherForecastData>(coordinates.Count);
        foreach (var (lat, lon) in coordinates)
        {
            results.Add(await GetWeatherAsync(lat, lon, cancellationToken));
        }
        return results;
    }

    private static WeatherForecastData ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        CurrentWeatherDto? currentDto = null;
        if (root.TryGetProperty("current", out var currentEl))
        {
            var temp = currentEl.GetProperty("temp_c").GetDouble();
            var feelsLike = currentEl.TryGetProperty("feelslike_c", out var fl) ? fl.GetDouble() : temp;
            var humidity = currentEl.TryGetProperty("humidity", out var hum) ? hum.GetDouble() : (double?)null;
            var windKph = currentEl.TryGetProperty("wind_kph", out var wk) ? wk.GetDouble() : (double?)null;
            var gustKph = currentEl.TryGetProperty("gust_kph", out var gk) ? gk.GetDouble() : (double?)null;
            string? conditionText = null;
            int? code = null;
            if (currentEl.TryGetProperty("condition", out var condEl))
            {
                conditionText = condEl.TryGetProperty("text", out var t) ? t.GetString() : null;
                code = condEl.TryGetProperty("code", out var c) ? c.GetInt32() : (int?)null;
            }

            currentDto = new CurrentWeatherDto(
                ObservedAt: DateTime.UtcNow,
                TemperatureC: temp,
                FeelsLikeC: feelsLike,
                HumidityPercent: humidity,
                WindSpeedKmh: windKph,
                WindGustsKmh: gustKph,
                Condition: conditionText,
                WeatherCode: code
            );
        }

        var points = new List<WeatherPoint>();
        var dailyList = new List<DailyForecastDto>();

        if (root.TryGetProperty("forecast", out var forecastEl) &&
            forecastEl.TryGetProperty("forecastday", out var forecastDayArray))
        {
            foreach (var dayEl in forecastDayArray.EnumerateArray())
            {
                DateOnly? date = null;
                if (dayEl.TryGetProperty("date", out var dStr) && DateOnly.TryParse(dStr.GetString(), CultureInfo.InvariantCulture, out var parsedDate))
                {
                    date = parsedDate;
                }

                double? maxTemp = null;
                double? minTemp = null;
                double? precipMm = null;
                int? rainChance = null;
                string? dayCond = null;

                if (dayEl.TryGetProperty("day", out var dayData))
                {
                    maxTemp = dayData.TryGetProperty("maxtemp_c", out var mt) ? mt.GetDouble() : (double?)null;
                    minTemp = dayData.TryGetProperty("mintemp_c", out var mnt) ? mnt.GetDouble() : (double?)null;
                    precipMm = dayData.TryGetProperty("totalprecip_mm", out var tp) ? tp.GetDouble() : (double?)null;
                    rainChance = dayData.TryGetProperty("daily_chance_of_rain", out var dc) ? dc.GetInt32() : (int?)null;
                    if (dayData.TryGetProperty("condition", out var dcEl) && dcEl.TryGetProperty("text", out var dt))
                    {
                        dayCond = dt.GetString();
                    }
                }

                if (date.HasValue)
                {
                    dailyList.Add(new DailyForecastDto(
                        Date: date.Value,
                        MinTemperatureC: minTemp,
                        MaxTemperatureC: maxTemp,
                        PrecipitationProbability: rainChance,
                        PrecipitationMm: precipMm,
                        Condition: dayCond,
                        WeatherCode: null
                    ));
                }

                if (dayEl.TryGetProperty("hour", out var hourArray))
                {
                    foreach (var h in hourArray.EnumerateArray())
                    {
                        DateTime observed = DateTime.UtcNow;
                        if (h.TryGetProperty("time", out var hTime) &&
                            DateTime.TryParse(hTime.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedObserved))
                        {
                            observed = parsedObserved.ToUniversalTime();
                        }

                        var hTemp = h.TryGetProperty("temp_c", out var ht) ? ht.GetDouble() : (double?)null;
                        var hChance = h.TryGetProperty("chance_of_rain", out var hc) ? hc.GetDouble() : (double?)null;
                        var hPrecip = h.TryGetProperty("precip_mm", out var hp) ? hp.GetDouble() : (double?)null;
                        var hWind = h.TryGetProperty("wind_kph", out var hw) ? hw.GetDouble() : (double?)null;
                        var hHum = h.TryGetProperty("humidity", out var hh) ? hh.GetDouble() : (double?)null;
                        int? hCode = null;
                        if (h.TryGetProperty("condition", out var hCond) && hCond.TryGetProperty("code", out var hcCode))
                        {
                            hCode = hcCode.GetInt32();
                        }

                        points.Add(new WeatherPoint(
                            ObservedAt: observed,
                            TemperatureC: hTemp,
                            PrecipitationProbability: hChance,
                            PrecipitationMm: hPrecip,
                            WindSpeedKmh: hWind,
                            HumidityPercent: hHum,
                            WeatherCode: hCode
                        ));
                    }
                }
            }
        }

        return new WeatherForecastData(points, currentDto, dailyList);
    }
}
