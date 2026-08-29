using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Infrastructure.Services;

public class OpenMeteoWeatherProvider : IWeatherProvider
{
    public string Name => "open_meteo";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OpenMeteoWeatherProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _baseUrl = config.GetValue<string>("Weather:OpenMeteoBaseUrl") ?? "https://api.open-meteo.com/v1/forecast";
    }

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}&hourly=temperature_2m,precipitation_probability,precipitation,wind_speed_10m&forecast_days=2&timezone=UTC&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var forecastData = JsonSerializer.Deserialize<OpenMeteoResponse>(json);

        if (forecastData?.Hourly?.Time == null || forecastData.Hourly.Time.Count == 0)
        {
            throw new InvalidOperationException("Hava durumu verisi alınamadı.");
        }

        var hourly = forecastData.Hourly;
        var count = hourly.Time.Count;
        var points = new List<WeatherPoint>(count);

        for (int i = 0; i < count; i++)
        {
            var timeStr = hourly.Time[i];
            var time = DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var temp = i < hourly.Temperature2m.Count ? hourly.Temperature2m[i] : null;
            var prob = i < hourly.PrecipitationProbability.Count ? hourly.PrecipitationProbability[i] : null;
            var rain = i < hourly.Precipitation.Count ? hourly.Precipitation[i] : null;
            var wind = i < hourly.WindSpeed10m.Count ? hourly.WindSpeed10m[i] : null;

            points.Add(new WeatherPoint(time, temp, prob, rain, wind));
        }

        return points;
    }

    private class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }
    }

    private class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();

        [JsonPropertyName("temperature_2m")]
        public List<double?> Temperature2m { get; set; } = new();

        [JsonPropertyName("precipitation_probability")]
        public List<double?> PrecipitationProbability { get; set; } = new();

        [JsonPropertyName("precipitation")]
        public List<double?> Precipitation { get; set; } = new();

        [JsonPropertyName("wind_speed_10m")]
        public List<double?> WindSpeed10m { get; set; } = new();
    }
}
