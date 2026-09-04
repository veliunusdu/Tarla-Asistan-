using System.Net;
using FluentAssertions;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class WttrInWeatherProviderTests
{
    private const string SampleWttrResponse = """
    {
      "current_condition": [
        {
          "FeelsLikeC": "29",
          "humidity": "12",
          "temp_C": "35",
          "weatherCode": "113",
          "weatherDesc": [
            {
              "value": "Sunny"
            }
          ],
          "windspeedKmph": "22"
        }
      ],
      "weather": [
        {
          "date": "2026-09-04",
          "maxtempC": "35",
          "mintempC": "29",
          "totalSnow_cm": "0.0",
          "hourly": [
            {
              "time": "0",
              "tempC": "31",
              "chanceofrain": "0",
              "precipMM": "0.0",
              "windspeedKmph": "12",
              "humidity": "15",
              "weatherCode": "113"
            },
            {
              "time": "300",
              "tempC": "30",
              "chanceofrain": "5",
              "precipMM": "0.1",
              "windspeedKmph": "10",
              "humidity": "19",
              "weatherCode": "116"
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void ParseResponse_ParsesCurrentWeatherAndPointsCorrectly()
    {
        var data = WttrInWeatherProvider.ParseResponse(SampleWttrResponse);

        data.Should().NotBeNull();
        data.Current.Should().NotBeNull();
        data.Current!.TemperatureC.Should().Be(35);
        data.Current.FeelsLikeC.Should().Be(29);
        data.Current.HumidityPercent.Should().Be(12);
        data.Current.WindSpeedKmh.Should().Be(22);
        data.Current.Condition.Should().Be("Sunny");
        data.Current.WeatherCode.Should().Be(113);

        data.Points.Should().HaveCount(2);
        data.Points[0].TemperatureC.Should().Be(31);
        data.Points[0].PrecipitationProbability.Should().Be(0);
        data.Points[0].WindSpeedKmh.Should().Be(12);

        data.Points[1].TemperatureC.Should().Be(30);
        data.Points[1].PrecipitationProbability.Should().Be(5);
        data.Points[1].PrecipitationMm.Should().Be(0.1);
        data.Points[1].WindSpeedKmh.Should().Be(10);
    }

    [Fact]
    public async Task GetWeatherAsync_WhenServerReturnsOk_ReturnsParsedData()
    {
        var handler = new MockHttpMessageHandler(SampleWttrResponse, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var provider = new WttrInWeatherProvider(client);

        var result = await provider.GetWeatherAsync(37.355, 40.255);

        result.Points.Should().HaveCount(2);
        result.Current!.TemperatureC.Should().Be(35);
    }

    [Fact]
    public async Task GetWeatherAsync_WhenRateLimited_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler("Too Many Requests", HttpStatusCode.TooManyRequests);
        var client = new HttpClient(handler);
        var provider = new WttrInWeatherProvider(client);

        var act = () => provider.GetWeatherAsync(37.355, 40.255);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*429*");
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string response, HttpStatusCode statusCode)
        {
            _response = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responseMessage = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response)
            };
            return Task.FromResult(responseMessage);
        }
    }
}
