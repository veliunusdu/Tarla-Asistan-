using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

public class ProductionAuthenticationIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new("Production");
    private readonly HttpClient _client;

    public ProductionAuthenticationIntegrationTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ListCases_WhenAnonymousInProduction_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/cases");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListCases_WhenOnlyPilotHeadersAreProvidedInProduction_ShouldReturnUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases");
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Role", "Agronomist");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/v1/notifications")]
    [InlineData("GET", "/api/v1/activities/00000000-0000-0000-0000-000000000000")]
    [InlineData("GET", "/api/v1/farms/00000000-0000-0000-0000-000000000000/production-periods")]
    [InlineData("GET", "/api/v1/pilot/metrics")]
    [InlineData("POST", "/api/v1/ai/chat")]
    [InlineData("POST", "/api/v1/media")]
    public async Task ProtectedFeatureEndpoints_WhenAnonymousInProduction_ShouldReturnUnauthorized(
        string method,
        string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WhenQuerySpecifiesAnotherUserInProduction_ShouldUseJwtSubject()
    {
        var authenticatedUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905551112233",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };
        var anotherUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905554445566",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(authenticatedUser, anotherUser);
            await db.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/auth/me?userId={anotherUser.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.Services.GetRequiredService<IJwtService>().GenerateAccessToken(authenticatedUser));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        user!.Id.Should().Be(authenticatedUser.Id);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
