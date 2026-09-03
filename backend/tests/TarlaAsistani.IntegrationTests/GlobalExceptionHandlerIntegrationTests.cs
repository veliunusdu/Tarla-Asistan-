using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class GlobalExceptionHandlerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public GlobalExceptionHandlerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.MockWeatherProvider.Reset();
    }

    [Fact]
    public async Task HttpPipeline_WhenUnhandledExceptionOccurs_ShouldReturnStructuredJsonFromGlobalHandler()
    {
        // Arrange: Create farm
        var ownerId = Guid.NewGuid();
        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Hata Test Tarlası",
            Latitude: 39.0, Longitude: 35.0, SizeInHectares: 5.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 4, 1)
        ), CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = farmResult!["id"];

        // Setup mock to throw an unhandled InvalidOperationException
        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Hava durumu servisi bağlantı koptu."));

        // Act: Call weather endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/weather");
        request.Headers.Add("X-User-Id", ownerId.ToString());
        var response = await _client.SendAsync(request);

        // Assert: a weather-provider outage is a temporary service failure, not a conflict.
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("detail").GetString().Should().Be("Hava durumu şu anda alınamıyor. Daha sonra tekrar deneyin; bu sırada saha koşullarını yerinde kontrol edin.");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        doc.RootElement.TryGetProperty("trace_id", out _).Should().BeTrue();
    }
}
