using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace TarlaAsistani.IntegrationTests;

public class HealthEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOkAndDatabaseStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonObject>();
        content.Should().NotBeNull();
        content!["status"]!.GetValue<string>().Should().Be("ok");
        content["database"]!.GetValue<string>().Should().Be("ok");
    }

    [Fact]
    public async Task GetHealthLive_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonObject>();
        content.Should().NotBeNull();
        content!["status"]!.GetValue<string>().Should().Be("ok");
    }

    [Fact]
    public async Task GetHealthReady_ShouldReturnOkAndDatabaseStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonObject>();
        content.Should().NotBeNull();
        content!["status"]!.GetValue<string>().Should().Be("ok");
        content["database"]!.GetValue<string>().Should().Be("ok");
        content["firebase"]!.GetValue<string>().Should().Be("not_configured");
    }
}
