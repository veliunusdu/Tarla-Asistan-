using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class OpenMeteoWeatherProviderTests
{
    private const string SampleSevenDayOpenMeteoResponse = """
    {
      "current": {
        "time": "2026-09-12T10:00",
        "temperature_2m": 24.5,
        "apparent_temperature": 23.8,
        "relative_humidity_2m": 45,
        "precipitation": 0.0,
        "weather_code": 1,
        "wind_speed_10m": 12.0,
        "wind_gusts_10m": 18.0
      },
      "hourly": {
        "time": ["2026-09-12T00:00", "2026-09-12T01:00"],
        "temperature_2m": [18.0, 17.5],
        "relative_humidity_2m": [60, 62],
        "precipitation_probability": [0, 0],
        "precipitation": [0.0, 0.0],
        "weather_code": [0, 0],
        "wind_speed_10m": [8.0, 7.5]
      },
      "daily": {
        "time": [
          "2026-09-12",
          "2026-09-13",
          "2026-09-14",
          "2026-09-15",
          "2026-09-16",
          "2026-09-17",
          "2026-09-18"
        ],
        "weather_code": [1, 2, 61, 0, 3, 45, 80],
        "temperature_2m_max": [26.0, 25.5, 21.0, 23.0, 24.0, 22.0, 20.0],
        "temperature_2m_min": [14.0, 15.0, 13.0, 12.0, 13.5, 11.0, 10.0],
        "precipitation_sum": [0.0, 0.5, 8.2, 0.0, 0.0, 0.2, 4.5],
        "precipitation_probability_max": [10, 35, 85, 5, 15, 20, 70]
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
    public async Task GetWeatherAsync_RequestsConfiguredSevenForecastDays()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleSevenDayOpenMeteoResponse)
            };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:ForecastDays"] = "7"
            })
            .Build();

        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("forecast_days=7");
    }

    [Fact]
    public async Task GetWeatherAsync_MapsSevenDayForecastResponseCorrectly()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleSevenDayOpenMeteoResponse)
        });

        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Should().NotBeNull();
        result.Daily.Should().NotBeNull();
        result.Daily!.Count.Should().Be(7);

        var firstDay = result.Daily[0];
        firstDay.Date.Should().Be(new DateOnly(2026, 9, 12));
        firstDay.MaxTemperatureC.Should().Be(26.0);
        firstDay.MinTemperatureC.Should().Be(14.0);
        firstDay.PrecipitationMm.Should().Be(0.0);
        firstDay.PrecipitationProbability.Should().Be(10);
        firstDay.WeatherCode.Should().Be(1);

        var lastDay = result.Daily[6];
        lastDay.Date.Should().Be(new DateOnly(2026, 9, 18));
        lastDay.MaxTemperatureC.Should().Be(20.0);
        lastDay.MinTemperatureC.Should().Be(10.0);
        lastDay.PrecipitationProbability.Should().Be(70);
    }

    [Theory]
    [InlineData("10", 7)]
    [InlineData("8", 7)]
    [InlineData("7", 7)]
    [InlineData("5", 5)]
    [InlineData("1", 1)]
    [InlineData("0", 1)]
    [InlineData("-3", 1)]
    [InlineData(null, 7)]
    public void WeatherDefaults_GetForecastDays_EnforcesBounds(string? configuredValue, int expectedDays)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:ForecastDays"] = configuredValue
            })
            .Build();

        var days = WeatherDefaults.GetForecastDays(config);
        days.Should().Be(expectedDays);
    }

    [Fact]
    public async Task GetWeatherAsync_WhenProviderReturnsFewerDays_PreservesAvailableDays()
    {
        const string threeDayResponse = """
        {
          "hourly": {
            "time": ["2026-09-12T00:00"],
            "temperature_2m": [18.0]
          },
          "daily": {
            "time": ["2026-09-12", "2026-09-13", "2026-09-14"],
            "weather_code": [1, 2, 3],
            "temperature_2m_max": [25.0, 24.0, 23.0],
            "temperature_2m_min": [14.0, 13.0, 12.0],
            "precipitation_sum": [0.0, 0.0, 1.2],
            "precipitation_probability_max": [5, 10, 40]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(threeDayResponse)
        });

        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Daily.Should().NotBeNull();
        result.Daily!.Count.Should().Be(3);
        result.Daily.Select(d => d.Date).Should().Equal(
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 13),
            new DateOnly(2026, 9, 14)
        );
    }

    [Fact]
    public async Task GetWeatherAsync_OrdersDaysChronologicallyAndDeduplicates()
    {
        const string duplicateUnsortedResponse = """
        {
          "hourly": {
            "time": ["2026-09-12T00:00"],
            "temperature_2m": [18.0]
          },
          "daily": {
            "time": ["2026-09-14", "2026-09-12", "2026-09-12", "2026-09-13"],
            "weather_code": [3, 1, 1, 2],
            "temperature_2m_max": [22.0, 25.0, 25.0, 24.0],
            "temperature_2m_min": [12.0, 14.0, 14.0, 13.0],
            "precipitation_sum": [1.0, 0.0, 0.0, 0.0],
            "precipitation_probability_max": [30, 5, 5, 10]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(duplicateUnsortedResponse)
        });

        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Daily.Should().NotBeNull();
        result.Daily!.Count.Should().Be(3);
        result.Daily.Select(d => d.Date).Should().Equal(
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 13),
            new DateOnly(2026, 9, 14)
        );
    }

    [Fact]
    public async Task GetWeatherAsync_SkipsMalformedDailyEntryWithoutFailingEntireResponse()
    {
        const string malformedEntryResponse = """
        {
          "current": {
            "time": "2026-09-12T10:00",
            "temperature_2m": 24.5
          },
          "hourly": {
            "time": ["2026-09-12T00:00"],
            "temperature_2m": [18.0]
          },
          "daily": {
            "time": ["2026-09-12", "invalid-date-format", "2026-09-14"],
            "weather_code": [1, 2, 3],
            "temperature_2m_max": [25.0, 99.0, 23.0],
            "temperature_2m_min": [14.0, 99.0, 12.0],
            "precipitation_sum": [0.0, 99.0, 1.2],
            "precipitation_probability_max": [5, 99, 40]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(malformedEntryResponse)
        });

        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Should().NotBeNull();
        result.Points.Should().NotBeEmpty();
        result.Daily.Should().NotBeNull();
        result.Daily!.Count.Should().Be(2);
        result.Daily.Select(d => d.Date).Should().Equal(
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 14)
        );
    }

    [Fact]
    public async Task GetWeatherAsync_WhenDailyMissing_DoesNotThrowAndReturnsPoints()
    {
        const string missingDailyResponse = """
        {
          "current": {
            "time": "2026-09-12T10:00",
            "temperature_2m": 24.5
          },
          "hourly": {
            "time": ["2026-09-12T00:00"],
            "temperature_2m": [18.0]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(missingDailyResponse)
        });

        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Should().NotBeNull();
        result.Points.Should().NotBeEmpty();
        result.Daily.Should().BeNull();
    }
}
