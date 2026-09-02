using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Open-Meteo weather provider with:
/// - Configurable BaseUrl (development free endpoint → commercial customer endpoint via config only)
/// - Optional API key (never source-controlled; read from config/env)
/// - Configurable TimeoutSeconds
/// - Batch multi-coordinate support (Open-Meteo supports multiple lat/lon in one request)
/// </summary>
public class OpenMeteoWeatherProvider : IWeatherProvider
{
    public string Name => "open_meteo";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public OpenMeteoWeatherProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;

        // BaseUrl: configurable, defaults to free Open-Meteo endpoint
        _baseUrl = FirstConfiguredValue(
            config["Weather:BaseUrl"],
            config["Weather:OpenMeteoBaseUrl"],
            config["OPEN_METEO_BASE_URL"],
            Environment.GetEnvironmentVariable("OPEN_METEO_BASE_URL"))
            ?? "https://api.open-meteo.com/v1/forecast";

        // ApiKey: optional (free tier has none; commercial tier requires one)
        // NEVER hard-code; read from config or environment only
        _apiKey = FirstConfiguredValue(
            config["Weather:OpenMeteoApiKey"],
            config["OPEN_METEO_API_KEY"],
            Environment.GetEnvironmentVariable("OPEN_METEO_API_KEY"));
    }

    private static string? FirstConfiguredValue(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    // ── Single-farm ──────────────────────────────────────────────────────────

    public async Task<List<WeatherPoint>> ForecastAsync(
        double latitude, double longitude,
        CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(
        double latitude, double longitude,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(latitude, longitude);

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("Hava durumu servisi istek sınırına ulaştı (429).");
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseSingleResponse(json);
    }

    // ── Batch multi-farm ─────────────────────────────────────────────────────

    /// <summary>
    /// Fetches weather for multiple (lat, lon) pairs in a single HTTP request
    /// using Open-Meteo's array parameter support.
    ///
    /// Returns results in the same order as the input coordinates.
    /// Falls back to individual requests if Open-Meteo batch response cannot be parsed.
    /// </summary>
    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        if (coordinates.Count == 0) return new List<WeatherForecastData>();
        if (coordinates.Count == 1)
        {
            var single = await GetWeatherAsync(
                coordinates[0].Latitude,
                coordinates[0].Longitude,
                cancellationToken);
            return new List<WeatherForecastData> { single };
        }

        // Open-Meteo supports: latitude=x,y&longitude=a,b for multiple locations
        var latStr = string.Join(",", coordinates.Select(c => c.Latitude.ToString(CultureInfo.InvariantCulture)));
        var lonStr = string.Join(",", coordinates.Select(c => c.Longitude.ToString(CultureInfo.InvariantCulture)));
        var url = BuildBatchUrl(latStr, lonStr);

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to individual requests
                return await FallbackToIndividualAsync(coordinates, cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            // Open-Meteo returns a JSON array when multiple locations are requested
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var results = new List<WeatherForecastData>(coordinates.Count);
                var i = 0;
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var elementJson = element.GetRawText();
                    results.Add(ParseSingleResponse(elementJson));
                    i++;
                }
                // Pad missing results (should not happen in normal operation)
                while (results.Count < coordinates.Count)
                    results.Add(new WeatherForecastData(new List<WeatherPoint>()));
                return results;
            }
            else
            {
                // Single response (shouldn't happen with multiple coords, but handle gracefully)
                var single = ParseSingleResponse(json);
                return Enumerable.Repeat(single, coordinates.Count).ToList();
            }
        }
        catch
        {
            // Batch failed — fall back to individual requests (N+1 fallback, bounded by caller)
            return await FallbackToIndividualAsync(coordinates, cancellationToken);
        }
    }

    private async Task<List<WeatherForecastData>> FallbackToIndividualAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken)
    {
        var results = new List<WeatherForecastData>(coordinates.Count);
        foreach (var (lat, lon) in coordinates)
        {
            try
            {
                results.Add(await GetWeatherAsync(lat, lon, cancellationToken));
            }
            catch
            {
                results.Add(new WeatherForecastData(new List<WeatherPoint>()));
            }
        }
        return results;
    }

    // ── URL builders ─────────────────────────────────────────────────────────

    private string BuildUrl(double lat, double lon)
    {
        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.ToString(CultureInfo.InvariantCulture);
        return BuildBatchUrl(latStr, lonStr);
    }

    private string BuildBatchUrl(string latStr, string lonStr)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_baseUrl);
        sb.Append($"?latitude={latStr}&longitude={lonStr}");
        sb.Append("&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_gusts_10m");
        sb.Append("&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,precipitation,weather_code,wind_speed_10m");
        sb.Append("&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max");
        sb.Append("&forecast_days=3&timezone=UTC&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm");

        // Attach API key if configured (commercial Open-Meteo customer endpoint)
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            sb.Append($"&apikey={_apiKey}");
        }

        return sb.ToString();
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private static WeatherForecastData ParseSingleResponse(string json)
    {
        var forecastData = JsonSerializer.Deserialize<OpenMeteoResponse>(json);

        if (forecastData?.Hourly?.Time == null || forecastData.Hourly.Time.Count == 0)
        {
            throw new InvalidOperationException("Hava durumu verisi alınamadı.");
        }

        // Current weather
        CurrentWeatherDto? currentDto = null;
        if (forecastData.Current != null)
        {
            var observedAt = DateTime.TryParse(forecastData.Current.Time, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedObs)
                ? parsedObs
                : DateTime.UtcNow;

            currentDto = new CurrentWeatherDto(
                ObservedAt: observedAt,
                TemperatureC: forecastData.Current.Temperature2m,
                FeelsLikeC: forecastData.Current.ApparentTemperature,
                HumidityPercent: forecastData.Current.RelativeHumidity2m,
                WindSpeedKmh: forecastData.Current.WindSpeed10m,
                WindGustsKmh: forecastData.Current.WindGusts10m,
                Condition: WmoWeatherCodeHelper.GetConditionDescription(forecastData.Current.WeatherCode),
                WeatherCode: forecastData.Current.WeatherCode
            );
        }

        // Hourly points
        var hourly = forecastData.Hourly;
        var count = hourly.Time.Count;
        var points = new List<WeatherPoint>(count);

        for (int i = 0; i < count; i++)
        {
            var timeStr = hourly.Time[i];
            var time = DateTime.Parse(timeStr, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var temp = i < hourly.Temperature2m.Count ? hourly.Temperature2m[i] : null;
            var prob = i < hourly.PrecipitationProbability.Count ? hourly.PrecipitationProbability[i] : null;
            var rain = i < hourly.Precipitation.Count ? hourly.Precipitation[i] : null;
            var wind = i < hourly.WindSpeed10m.Count ? hourly.WindSpeed10m[i] : null;
            var humidity = hourly.RelativeHumidity2m != null && i < hourly.RelativeHumidity2m.Count
                ? hourly.RelativeHumidity2m[i]
                : null;
            var code = hourly.WeatherCode != null && i < hourly.WeatherCode.Count
                ? hourly.WeatherCode[i]
                : null;

            points.Add(new WeatherPoint(time, temp, prob, rain, wind, humidity, code));
        }

        // Daily summaries
        List<DailyForecastDto>? dailyList = null;
        if (forecastData.Daily?.Time != null && forecastData.Daily.Time.Count > 0)
        {
            dailyList = new List<DailyForecastDto>(forecastData.Daily.Time.Count);
            for (int i = 0; i < forecastData.Daily.Time.Count; i++)
            {
                var dateStr = forecastData.Daily.Time[i];
                var date = DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var parsedDate)
                    ? parsedDate
                    : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i));

                var minTemp = i < forecastData.Daily.Temperature2mMin.Count ? forecastData.Daily.Temperature2mMin[i] : null;
                var maxTemp = i < forecastData.Daily.Temperature2mMax.Count ? forecastData.Daily.Temperature2mMax[i] : null;
                var precipProb = i < forecastData.Daily.PrecipitationProbabilityMax.Count ? forecastData.Daily.PrecipitationProbabilityMax[i] : null;
                var precipSum = i < forecastData.Daily.PrecipitationSum.Count ? forecastData.Daily.PrecipitationSum[i] : null;
                var code = i < forecastData.Daily.WeatherCode.Count ? forecastData.Daily.WeatherCode[i] : null;

                dailyList.Add(new DailyForecastDto(
                    Date: date,
                    MinTemperatureC: minTemp,
                    MaxTemperatureC: maxTemp,
                    PrecipitationProbability: precipProb,
                    PrecipitationMm: precipSum,
                    Condition: WmoWeatherCodeHelper.GetConditionDescription(code),
                    WeatherCode: code
                ));
            }
        }

        return new WeatherForecastData(points, currentDto, dailyList);
    }

    // ── Response model ────────────────────────────────────────────────────────

    private class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public OpenMeteoCurrent? Current { get; set; }

        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }

        [JsonPropertyName("daily")]
        public OpenMeteoDaily? Daily { get; set; }
    }

    private class OpenMeteoCurrent
    {
        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public double? Temperature2m { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public double? RelativeHumidity2m { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public double? ApparentTemperature { get; set; }

        [JsonPropertyName("precipitation")]
        public double? Precipitation { get; set; }

        [JsonPropertyName("weather_code")]
        public int? WeatherCode { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double? WindSpeed10m { get; set; }

        [JsonPropertyName("wind_gusts_10m")]
        public double? WindGusts10m { get; set; }
    }

    private class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();

        [JsonPropertyName("temperature_2m")]
        public List<double?> Temperature2m { get; set; } = new();

        [JsonPropertyName("relative_humidity_2m")]
        public List<double?> RelativeHumidity2m { get; set; } = new();

        [JsonPropertyName("precipitation_probability")]
        public List<double?> PrecipitationProbability { get; set; } = new();

        [JsonPropertyName("precipitation")]
        public List<double?> Precipitation { get; set; } = new();

        [JsonPropertyName("weather_code")]
        public List<int?> WeatherCode { get; set; } = new();

        [JsonPropertyName("wind_speed_10m")]
        public List<double?> WindSpeed10m { get; set; } = new();
    }

    private class OpenMeteoDaily
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();

        [JsonPropertyName("temperature_2m_max")]
        public List<double?> Temperature2mMax { get; set; } = new();

        [JsonPropertyName("temperature_2m_min")]
        public List<double?> Temperature2mMin { get; set; } = new();

        [JsonPropertyName("precipitation_probability_max")]
        public List<double?> PrecipitationProbabilityMax { get; set; } = new();

        [JsonPropertyName("precipitation_sum")]
        public List<double?> PrecipitationSum { get; set; } = new();

        [JsonPropertyName("weather_code")]
        public List<int?> WeatherCode { get; set; } = new();
    }
}
