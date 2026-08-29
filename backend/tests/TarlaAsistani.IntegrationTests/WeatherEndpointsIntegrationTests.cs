using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class WeatherEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public WeatherEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetFarmWeather_WhenProviderReturnsForecast_ShouldReturnPointsAndRisks()
    {
        // 1. Create a Farm
        var ownerId = Guid.NewGuid();
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Hava Durumu Test Tarlası",
            Latitude: 37.87,
            Longitude: 32.49,
            SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 4, 1)
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest, CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Setup mock weather provider with frost data
        var now = DateTime.UtcNow;
        var forecastPoints = new List<WeatherPoint>
        {
            new(now.AddHours(2), 6.0, 10, 0, 10),
            new(now.AddHours(6), -1.5, 5, 0, 8), // Frost!
            new(now.AddHours(12), 12.0, 0, 0, 15)
        };

        _factory.MockWeatherProvider.Setup(w => w.Name).Returns("Open-Meteo");
        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecastPoints);

        // 3. Request weather forecast
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var weatherResult = await response.Content.ReadFromJsonAsync<FarmWeatherResponseDto>(CustomWebApplicationFactory.JsonOptions);
        weatherResult.Should().NotBeNull();
        weatherResult!.Points.Should().NotBeEmpty();
        weatherResult.Risks.Should().ContainSingle(r => r.RiskType == "FROST");
    }
}
