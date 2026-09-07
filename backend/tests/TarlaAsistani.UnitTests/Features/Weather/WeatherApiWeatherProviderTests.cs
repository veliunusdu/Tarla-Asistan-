using System.Net;
using System.Globalization;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

[CollectionDefinition("Weather API environment", DisableParallelization = true)]
public class WeatherApiEnvironmentCollection;

[Collection("Weather API environment")]
public class WeatherApiWeatherProviderTests : IDisposable
{
    private readonly string? _originalApiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");

    public WeatherApiWeatherProviderTests()
    {
        // Isolate process-wide credentials from both developer settings and other tests.
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", null);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("WEATHER_API_KEY", _originalApiKey);

    private const string SampleWeatherApiResponse = """
    {
      "location": {
        "name": "Konya",
        "lat": 37.87,
        "lon": 32.48,
        "tz_id": "Europe/Istanbul",
        "localtime": "2026-09-04 15:00"
      },
      "current": {
        "last_updated_epoch": 1788522300,
        "last_updated": "2026-09-04 14:45",
        "temp_c": 28.5,
        "feelslike_c": 27.2,
        "humidity": 22,
        "wind_kph": 15.0,
        "gust_kph": 25.0,
        "precip_mm": 0.0,
        "condition": {
          "text": "Güneşli",
          "code": 1000
        }
      },
      "forecast": {
        "forecastday": [
          {
            "date": "2026-09-04",
            "day": {
              "maxtemp_c": 30.0,
              "mintemp_c": 16.0,
              "totalprecip_mm": 0.0,
              "daily_chance_of_rain": 10,
              "condition": {
                "text": "Güneşli",
                "code": 1000
              }
            },
            "hour": [
              {
                "time_epoch": 1788512400,
                "time": "2026-09-04 12:00",
                "temp_c": 27.0,
                "chance_of_rain": 10,
                "precip_mm": 0.0,
                "wind_kph": 12.0,
                "humidity": 25,
                "condition": {
                  "text": "Açık",
                  "code": 1000
                }
              },
              {
                "time_epoch": 1788516000,
                "time": "2026-09-04 13:00",
                "temp_c": 28.5,
                "chance_of_rain": 10,
                "precip_mm": 0.0,
                "wind_kph": 15.0,
                "humidity": 22,
                "condition": {
                  "text": "Güneşli",
                  "code": 1000
                }
              }
            ]
          }
        ]
      }
    }
    """;

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    [Fact]
    public async Task GetWeatherAsync_ParsesWeatherApiResponse_Correctly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "test-api-key"
            })
            .Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.ToString().Should().Contain("key=test-api-key");
            req.RequestUri.ToString().Should().Contain("q=37.8700,32.4800");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleWeatherApiResponse)
            };
        });

        var client = new HttpClient(handler);
        var provider = new WeatherApiWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Should().NotBeNull();
        result.Current.Should().NotBeNull();
        result.Current!.TemperatureC.Should().Be(28.5);
        result.Current.FeelsLikeC.Should().Be(27.2);
        result.Current.Condition.Should().Be("Güneşli");
        result.Current.HumidityPercent.Should().Be(22);
        result.Current.WindSpeedKmh.Should().Be(15.0);
        result.Current.WindGustsKmh.Should().Be(25.0);
        result.Current.WeatherCode.Should().Be(1000);
        result.Points.Should().HaveCount(2);
        result.Daily.Should().HaveCount(1);
        result.Daily![0].MaxTemperatureC.Should().Be(30.0);
        result.Daily![0].MinTemperatureC.Should().Be(16.0);
        result.Daily![0].PrecipitationProbability.Should().Be(10);
        result.Daily![0].Condition.Should().Be("Güneşli");
    }

    [Fact]
    public async Task GetWeatherAsync_UsesEpochsForUtcObservationTimes_NotLocalTimesOrServerClock()
    {
        var result = await ReadResponseAsync(SampleWeatherApiResponse);

        result.Current!.ObservedAt.Should().Be(new DateTime(2026, 9, 4, 11, 45, 0, DateTimeKind.Utc));
        result.Current.ObservedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        result.Points.Select(p => p.ObservedAt).Should().Equal(
            new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc));
        result.Points.Should().OnlyContain(p => p.ObservedAt.Kind == DateTimeKind.Utc);
        result.Daily![0].Date.Should().Be(new DateOnly(2026, 9, 4));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Invalid/TimeZone")]
    public async Task GetWeatherAsync_ValidEpochsDoNotDependOnLocationTimeZoneOrLocalText(string? timeZone)
    {
        var json = JsonNode.Parse(SampleWeatherApiResponse)!;
        json["location"]!["tz_id"] = timeZone;
        json["current"]!["last_updated"] = "invalid";
        json["forecast"]!["forecastday"]![0]!["hour"]![0]!["time"] = "invalid";

        var result = await ReadResponseAsync(json.ToJsonString());

        result.Current!.ObservedAt.Should().Be(new DateTime(2026, 9, 4, 11, 45, 0, DateTimeKind.Utc));
        result.Points[0].ObservedAt.Should().Be(new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("Europe/Istanbul", "2026-09-04 12:00", "2026-09-04T09:00:00Z")]
    [InlineData("Europe/Istanbul", "2026-09-04 00:15", "2026-09-03T21:15:00Z")]
    [InlineData("America/New_York", "2026-07-01 12:00", "2026-07-01T16:00:00Z")]
    [InlineData("America/New_York", "2026-01-01 12:00", "2026-01-01T17:00:00Z")]
    [InlineData("Asia/Kathmandu", "2026-09-04 12:00", "2026-09-04T06:15:00Z")]
    public async Task GetWeatherAsync_WithoutEpochs_ConvertsUsingLocationTimeZone(string timeZone, string localTime, string expectedUtc)
    {
        var json = JsonNode.Parse(SampleWeatherApiResponse)!;
        json["location"]!["tz_id"] = timeZone;
        var current = json["current"]!.AsObject();
        current.Remove("last_updated_epoch");
        current["last_updated"] = localTime;
        var hour = json["forecast"]!["forecastday"]![0]!["hour"]![0]!.AsObject();
        hour.Remove("time_epoch");
        hour["time"] = localTime;

        var result = await ReadResponseAsync(json.ToJsonString());

        var expected = DateTime.Parse(expectedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        result.Current!.ObservedAt.Should().Be(expected);
        result.Points[0].ObservedAt.Should().Be(expected);
        result.Points[0].ObservedAt.Kind.Should().Be(DateTimeKind.Utc);
        result.Daily![0].Date.Should().Be(new DateOnly(2026, 9, 4));
    }

    [Theory]
    [InlineData(null, "2026-09-04 12:00")]
    [InlineData("Invalid/TimeZone", "2026-09-04 12:00")]
    [InlineData("Europe/Istanbul", null)]
    [InlineData("Europe/Istanbul", "invalid")]
    [InlineData("America/New_York", "2026-03-08 02:30")]
    [InlineData("America/New_York", "2026-11-01 01:30")]
    public async Task GetWeatherAsync_UnresolvableForecastTime_RejectsInsteadOfInventingUtcOrNow(string? timeZone, string? localTime)
    {
        var json = JsonNode.Parse(SampleWeatherApiResponse)!;
        json["location"]!["tz_id"] = timeZone;
        var hour = json["forecast"]!["forecastday"]![0]!["hour"]![0]!.AsObject();
        hour.Remove("time_epoch");
        hour["time"] = localTime;

        var act = () => ReadResponseAsync(json.ToJsonString());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"invalid\"")]
    [InlineData("9223372036854775807")]
    public async Task GetWeatherAsync_InvalidEpoch_UsesValidZonedLocalTime(string epochJson)
    {
        var json = JsonNode.Parse(SampleWeatherApiResponse)!;
        json["current"]!["last_updated_epoch"] = JsonNode.Parse(epochJson);
        json["forecast"]!["forecastday"]![0]!["hour"]![0]!["time_epoch"] = JsonNode.Parse(epochJson);

        var result = await ReadResponseAsync(json.ToJsonString());

        result.Current!.ObservedAt.Should().Be(new DateTime(2026, 9, 4, 11, 45, 0, DateTimeKind.Utc));
        result.Points[0].ObservedAt.Should().Be(new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetWeatherAsync_MissingCurrentTimestamp_RejectsInsteadOfUsingServerClock()
    {
        var json = JsonNode.Parse(SampleWeatherApiResponse)!;
        json["current"]!.AsObject().Remove("last_updated_epoch");
        json["current"]!.AsObject().Remove("last_updated");

        var act = () => ReadResponseAsync(json.ToJsonString());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static async Task<WeatherForecastData> ReadResponseAsync(string json)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Weather:WeatherApiKey"] = "test-api-key" })
            .Build();
        using var client = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));
        return await new WeatherApiWeatherProvider(client, config).GetWeatherAsync(37.87, 32.48);
    }

    [Fact]
    public void IsConfigured_ReturnsExpectedValue_BasedOnApiKey()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var unconfiguredProvider = new WeatherApiWeatherProvider(new HttpClient(), emptyConfig);
        unconfiguredProvider.IsConfigured.Should().BeFalse();

        var configuredConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "valid-key"
            })
            .Build();
        var configuredProvider = new WeatherApiWeatherProvider(new HttpClient(), configuredConfig);
        configuredProvider.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "environment-test-key")]
    [InlineData("", "environment-test-key")]
    [InlineData(" \t ", "environment-test-key")]
    [InlineData("configured-test-key", "configured-test-key")]
    public async Task GetWeatherAsync_ResolvesUsableApiKey_AndSendsItInRequest(string? configuredKey, string expectedKey)
    {
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "environment-test-key");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = configuredKey
            })
            .Build();
        Uri? requestedUri = null;
        using var client = new HttpClient(new MockHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleWeatherApiResponse)
            };
        }));
        var provider = new WeatherApiWeatherProvider(client, config);

        provider.IsConfigured.Should().BeTrue();
        var result = await provider.GetWeatherAsync(37.87, 32.48);

        requestedUri.Should().NotBeNull();
        requestedUri!.Query.Should().Contain($"key={expectedKey}&");
        result.Current!.TemperatureC.Should().Be(28.5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task GetWeatherAsync_WhenBothKeysAreBlank_RejectsWithoutHttpRequest(string? environmentKey)
    {
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", environmentKey);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = ""
            })
            .Build();
        var requestCount = 0;
        using var client = new HttpClient(new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var provider = new WeatherApiWeatherProvider(client, config);

        provider.IsConfigured.Should().BeFalse();
        var act = () => provider.GetWeatherAsync(37.87, 32.48);

        await act.Should().ThrowAsync<InvalidOperationException>();
        requestCount.Should().Be(0);
    }

    [Fact]
    public async Task GetWeatherAsync_ThrowsInvalidOperationException_WhenApiKeyMissing()
    {
        var config = new ConfigurationBuilder().Build();
        var provider = new WeatherApiWeatherProvider(new HttpClient(), config);

        var act = async () => await provider.GetWeatherAsync(37.87, 32.48);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*yapılandırılmamış*");
    }

    [Fact]
    public async Task GetWeatherAsync_ThrowsInvalidOperationException_WhenRateLimited429()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "test-api-key"
            })
            .Build();

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var client = new HttpClient(handler);
        var provider = new WeatherApiWeatherProvider(client, config);

        var act = async () => await provider.GetWeatherAsync(37.87, 32.48);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*429*");
    }

    [Fact]
    public async Task ForecastAsync_DelegatesToGetWeatherAsync_ReturnsPoints()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "test-api-key"
            })
            .Build();

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleWeatherApiResponse)
        });

        var client = new HttpClient(handler);
        var provider = new WeatherApiWeatherProvider(client, config);

        var points = await provider.ForecastAsync(37.87, 32.48);

        points.Should().HaveCount(2);
        points[0].TemperatureC.Should().Be(27.0);
        points[1].TemperatureC.Should().Be(28.5);
    }

    [Fact]
    public async Task GetWeatherBatchAsync_ReturnsResultsForMultipleCoordinates()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "test-api-key"
            })
            .Build();

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleWeatherApiResponse)
        });

        var client = new HttpClient(handler);
        var provider = new WeatherApiWeatherProvider(client, config);

        var batchCoords = new List<(double, double)>
        {
            (37.87, 32.48),
            (38.00, 33.00)
        };

        var batchResult = await provider.GetWeatherBatchAsync(batchCoords);

        batchResult.Should().HaveCount(2);
        batchResult[0].Current!.TemperatureC.Should().Be(28.5);
        batchResult[1].Current!.TemperatureC.Should().Be(28.5);
    }
}
