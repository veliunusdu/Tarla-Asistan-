using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class WeatherApiWeatherProviderTests
{
    private const string SampleWeatherApiResponse = """
    {
      "location": {
        "name": "Konya",
        "lat": 37.87,
        "lon": 32.48,
        "localtime": "2026-09-04 15:00"
      },
      "current": {
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
