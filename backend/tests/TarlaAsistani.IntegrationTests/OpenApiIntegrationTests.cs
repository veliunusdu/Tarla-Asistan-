using System.Net;
using FluentAssertions;

namespace TarlaAsistani.IntegrationTests;

public class OpenApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public OpenApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerDocument_ShouldBeGeneratedSuccessfully()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
