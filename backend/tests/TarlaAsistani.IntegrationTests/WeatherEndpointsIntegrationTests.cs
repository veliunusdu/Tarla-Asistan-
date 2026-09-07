using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Infrastructure.Persistence;

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

    private async Task<Guid> CreateFarmAsync(Guid ownerId, double? lat = 37.87, double? lon = 32.49, string name = "Hava Durumu Test Tarlası")
    {
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: name,
            Latitude: lat,
            Longitude: lon,
            SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 4, 1)
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest, CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        return createResult!["id"];
    }

    [Fact]
    public async Task GetFarmWeather_WithPointsOnlyProvider_UsesCurrentHourInsteadOfFirstPoint()
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId, 39.3, 32.3);
        var now = DateTime.UtcNow;
        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(39.3, 32.3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeatherPoint>
            {
                new(now.AddHours(-12), 3, 0, 0, 1, 90),
                new(now.AddHours(8), 15, 0, 0, 2, 70),
                new(now, 25, 0, 0, 12, 40)
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var weather = await response.Content.ReadFromJsonAsync<FarmWeatherResponseDto>(CustomWebApplicationFactory.JsonOptions);
        weather!.Current!.TemperatureC.Should().Be(25);
        weather.Current.HumidityPercent.Should().Be(40);
        weather.Current.ObservedAt.Should().Be(now);
    }

    [Fact]
    public async Task GetFarmWeather_WithLegacySnapshotAndProviderFailure_ShouldNotAssumeItsLocation()
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId, 39.1, 32.1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.WeatherSnapshots.Add(new WeatherSnapshot
            {
                FarmId = farmId,
                Provider = "test",
                Payload = JsonSerializer.Serialize(new List<WeatherPoint> { new(DateTime.UtcNow, 7, 0, 0, 0) })
            });
            await db.SaveChangesAsync();
        }
        _factory.MockWeatherProvider
            .Setup(w => w.GetWeatherAsync(39.1, 32.1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Test provider outage"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData(37.1, 32.2, false)]
    [InlineData(39.2, 30.1, false)]
    [InlineData(37.1, 30.1, false)]
    [InlineData(39.2, 32.2, true)]
    public async Task GetFarmWeather_AfterLocationUpdate_OnlyUsesMatchingSnapshot(
        double latitude, double longitude, bool sameLocation)
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId, 39.2, 32.2);
        _factory.MockWeatherProvider
            .Setup(w => w.GetWeatherAsync(39.2, 32.2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecastData(new List<WeatherPoint> { new(DateTime.UtcNow, 7, 0, 0, 0) }));
        using var initialRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        initialRequest.Headers.Add("X-User-Id", ownerId.ToString());
        (await _client.SendAsync(initialRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/farms/{farmId}");
        update.Headers.Add("X-User-Id", ownerId.ToString());
        update.Content = JsonContent.Create(new { latitude, longitude }, options: CustomWebApplicationFactory.JsonOptions);
        (await _client.SendAsync(update)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Force the persisted fallback path rather than accepting a memory-cache hit.
        _factory.Services.GetRequiredService<IMemoryCache>().Remove($"weather:{farmId}:{39.2:F6}:{32.2:F6}");
        _factory.MockWeatherProvider
            .Setup(w => w.GetWeatherAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Test provider outage"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(sameLocation ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        if (sameLocation)
        {
            var weather = await response.Content.ReadFromJsonAsync<FarmWeatherResponseDto>(CustomWebApplicationFactory.JsonOptions);
            weather!.IsStale.Should().BeTrue();
            weather.Points.Single().TemperatureC.Should().Be(7);
        }
    }

    [Fact]
    public async Task GetFarmWeather_WhenProviderReturnsForecast_ShouldReturnPointsAndRisks()
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId);

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

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var weatherResult = await response.Content.ReadFromJsonAsync<FarmWeatherResponseDto>(CustomWebApplicationFactory.JsonOptions);
        weatherResult.Should().NotBeNull();
        weatherResult!.Points.Should().NotBeEmpty();
        weatherResult.Risks.Should().ContainSingle(r => r.RiskType == "FROST");
    }

    [Fact]
    public async Task GetFarmWeather_WhenAnonymous_ShouldReturn401Unauthorized()
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId);

        // No X-User-Id header
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFarmWeather_CrossUserSecurity_WhenUserBRequestsUserAFarm_ShouldReturn404NotFound()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var farmA = await CreateFarmAsync(userA, name: "User A Farm");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmA}/weather");
        request.Headers.Add("X-User-Id", userB.ToString()); // User B trying to access User A's farm weather

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFarmWeather_WhenFarmHasNoCoordinates_ShouldReturn422UnprocessableEntity()
    {
        var ownerId = Guid.NewGuid();
        var farmId = await CreateFarmAsync(ownerId, lat: null, lon: null, name: "Konumsuz Tarla");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("konum");
    }

    [Fact]
    public async Task GetFarmWeather_WhenCalledTwice_ShouldHitCacheAndNotCallProviderAgain()
    {
        var ownerId = Guid.NewGuid();
        const double targetLat = 38.123;
        const double targetLon = 33.456;
        var farmId = await CreateFarmAsync(ownerId, lat: targetLat, lon: targetLon, name: "Cache Test Farm");

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint> { new(now.AddHours(1), 22.0, 0, 0, 5) };

        var callCount = 0;
        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(points)
            .Callback<double, double, CancellationToken>((lat, lon, ct) => callCount++);

        // First call -> cache miss, calls provider
        var request1 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request1.Headers.Add("X-User-Id", ownerId.ToString());
        var res1 = await _client.SendAsync(request1);
        var body1 = await res1.Content.ReadAsStringAsync();
        res1.StatusCode.Should().Be(HttpStatusCode.OK, because: body1);
        callCount.Should().Be(1);

        // Second call -> cache hit, does NOT call provider
        var request2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request2.Headers.Add("X-User-Id", ownerId.ToString());
        var res2 = await _client.SendAsync(request2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(1); // Still 1! Cache hit!
    }

    [Fact]
    public async Task GetFarmWeather_CallsProviderWithFarmCoordinates()
    {
        var ownerId = Guid.NewGuid();
        const double specificLat = 39.925;
        const double specificLon = 32.836;
        var farmId = await CreateFarmAsync(ownerId, lat: specificLat, lon: specificLon, name: "Exact Coordinates Farm");

        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint> { new(now.AddHours(1), 18.0, 10, 0, 12) };

        double? capturedLat = null;
        double? capturedLon = null;

        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(points)
            .Callback<double, double, CancellationToken>((lat, lon, ct) =>
            {
                capturedLat = lat;
                capturedLon = lon;
            });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        capturedLat.Should().Be(specificLat);
        capturedLon.Should().Be(specificLon);
    }
}
