using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Free, keyless weather provider using wttr.in as an automatic resilient fallback.
/// Ideal for environments like Render where shared IPs might be rate-limited by Open-Meteo.
/// </summary>
public class WttrInWeatherProvider : IWeatherProvider
{
    public string Name => "wttr_in";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public WttrInWeatherProvider(HttpClient httpClient, IConfiguration? config = null)
    {
        _httpClient = httpClient;
        _baseUrl = config?["Weather:WttrInBaseUrl"]
            ?? Environment.GetEnvironmentVariable("WTTR_IN_BASE_URL")
            ?? "https://wttr.in";
    }

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var latStr = latitude.ToString("F4", CultureInfo.InvariantCulture);
        var lonStr = longitude.ToString("F4", CultureInfo.InvariantCulture);
        var url = $"{_baseUrl.TrimEnd('/')}/{latStr},{lonStr}?format=j1";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("wttr.in hava durumu servisi istek sınırına ulaştı (429).");
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResponse(json);
    }

    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WeatherForecastData>();
        foreach (var (lat, lon) in coordinates)
        {
            results.Add(await GetWeatherAsync(lat, lon, cancellationToken));
        }
        return results;
    }

    public static WeatherForecastData ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        CurrentWeatherDto? current = null;
        if (root.TryGetProperty("current_condition", out var currentArr) &&
            currentArr.ValueKind == JsonValueKind.Array &&
            currentArr.GetArrayLength() > 0)
        {
            var c = currentArr[0];
            var tempC = ParseDouble(c, "temp_C");
            var feelsLikeC = ParseDouble(c, "FeelsLikeC");
            var humidity = ParseDouble(c, "humidity");
            var windKmh = ParseDouble(c, "windspeedKmph");
            var weatherCode = ParseInt(c, "weatherCode");
            string? condition = null;
            if (c.TryGetProperty("weatherDesc", out var descArr) &&
                descArr.ValueKind == JsonValueKind.Array &&
                descArr.GetArrayLength() > 0 &&
                descArr[0].TryGetProperty("value", out var descVal))
            {
                condition = descVal.GetString()?.Trim();
            }

            current = new CurrentWeatherDto(
                ObservedAt: DateTime.UtcNow,
                TemperatureC: tempC,
                FeelsLikeC: feelsLikeC,
                HumidityPercent: humidity,
                WindSpeedKmh: windKmh,
                WindGustsKmh: null,
                Condition: condition,
                WeatherCode: weatherCode
            );
        }

        var points = new List<WeatherPoint>();
        var dailyList = new List<DailyForecastDto>();

        if (root.TryGetProperty("weather", out var weatherArr) &&
            weatherArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var dayElem in weatherArr.EnumerateArray())
            {
                DateOnly? date = null;
                if (dayElem.TryGetProperty("date", out var dateProp) &&
                    DateOnly.TryParse(dateProp.GetString(), CultureInfo.InvariantCulture, out var parsedDate))
                {
                    date = parsedDate;
                }

                var maxTemp = ParseDouble(dayElem, "maxtempC");
                var minTemp = ParseDouble(dayElem, "mintempC");
                var totalPrecipMm = ParseDouble(dayElem, "totalSnow_cm");

                if (date.HasValue)
                {
                    dailyList.Add(new DailyForecastDto(
                        Date: date.Value,
                        MinTemperatureC: minTemp,
                        MaxTemperatureC: maxTemp,
                        PrecipitationProbability: null,
                        PrecipitationMm: totalPrecipMm,
                        Condition: null,
                        WeatherCode: null
                    ));
                }

                if (dayElem.TryGetProperty("hourly", out var hourlyArr) &&
                    hourlyArr.ValueKind == JsonValueKind.Array &&
                    date.HasValue)
                {
                    foreach (var h in hourlyArr.EnumerateArray())
                    {
                        var timeVal = ParseInt(h, "time") ?? 0;
                        var hour = Math.Clamp(timeVal / 100, 0, 23);
                        var minute = Math.Clamp(timeVal % 100, 0, 59);
                        var observedAt = DateTime.SpecifyKind(date.Value.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Utc);

                        var temp = ParseDouble(h, "tempC");
                        var precipProb = ParseDouble(h, "chanceofrain");
                        var precipMm = ParseDouble(h, "precipMM");
                        var wind = ParseDouble(h, "windspeedKmph");
                        var hum = ParseDouble(h, "humidity");
                        var code = ParseInt(h, "weatherCode");

                        points.Add(new WeatherPoint(
                            ObservedAt: observedAt,
                            TemperatureC: temp,
                            PrecipitationProbability: precipProb,
                            PrecipitationMm: precipMm,
                            WindSpeedKmh: wind,
                            HumidityPercent: hum,
                            WeatherCode: code
                        ));
                    }
                }
            }
        }

        return new WeatherForecastData(points, current, dailyList.Count > 0 ? dailyList : null);
    }

    private static double? ParseDouble(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
            if (prop.ValueKind == JsonValueKind.String &&
                double.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static int? ParseInt(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) return i;
            if (prop.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }
}
