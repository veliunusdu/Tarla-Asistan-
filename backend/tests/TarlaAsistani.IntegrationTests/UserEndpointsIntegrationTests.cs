using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "Security")]
public class UserEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public UserEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedUserAsync(Guid userId, string phone, UserRole role, string? fullName = null, string? province = null, string? district = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                PhoneNumber = phone,
                Role = role,
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    FullName = fullName ?? "Test User",
                    Province = province ?? "Ankara",
                    District = district ?? "Çankaya",
                    TermsAccepted = true,
                    NotificationsEnabled = true,
                }
            };
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ShouldReturnOwnProfile()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "+905559876543", UserRole.Farmer, fullName: "Fatma Çelik", province: "Konya", district: "Karatay");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-User-Id", userId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.FullName.Should().Be("Fatma Çelik");
        result.Province.Should().Be("Konya");
        result.District.Should().Be("Karatay");
        result.Role.Should().Be(UserRole.Farmer);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_ShouldPersistChangesInPostgreSqlAndNotAffectOtherUsers()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedUserAsync(userA, "+905550000001", UserRole.Farmer, fullName: "Kullanıcı A", province: "İzmir", district: "Bornova");
        await SeedUserAsync(userB, "+905550000002", UserRole.Farmer, fullName: "Kullanıcı B", province: "Antalya", district: "Alanya");

        // User A updates profile
        using var updateRequestA = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me");
        updateRequestA.Headers.Add("X-User-Id", userA.ToString());
        updateRequestA.Content = JsonContent.Create(new UpdateProfileApiRequest(
            UserId: userA,
            FullName: "Kullanıcı A Güncel",
            Province: "Manisa",
            District: "Salihli",
            TermsAccepted: true,
            NotificationsEnabled: false
        ), options: CustomWebApplicationFactory.JsonOptions);

        var responseA = await _client.SendAsync(updateRequestA);
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultA = await responseA.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        resultA.Should().NotBeNull();
        resultA!.FullName.Should().Be("Kullanıcı A Güncel");
        resultA.Province.Should().Be("Manisa");
        resultA.District.Should().Be("Salihli");
        resultA.NotificationsEnabled.Should().BeFalse();

        // Verify User B's profile was NOT modified
        using var getRequestB = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        getRequestB.Headers.Add("X-User-Id", userB.ToString());

        var responseB = await _client.SendAsync(getRequestB);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultB = await responseB.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        resultB.Should().NotBeNull();
        resultB!.FullName.Should().Be("Kullanıcı B");
        resultB.Province.Should().Be("Antalya");
        resultB.District.Should().Be("Alanya");
        resultB.NotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RoleAreas_ShouldEnforceFarmerAndAgronomistAccess()
    {
        var farmerId = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();

        await SeedUserAsync(farmerId, "+905550000003", UserRole.Farmer);
        await SeedUserAsync(agronomistId, "+905550000004", UserRole.Agronomist);

        // Farmer accessing farmer-area -> 200 OK
        using var farmerInFarmerArea = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/farmer-area");
        farmerInFarmerArea.Headers.Add("X-User-Id", farmerId.ToString());
        var res1 = await _client.SendAsync(farmerInFarmerArea);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Farmer accessing agronomist-area -> 403 Forbidden
        using var farmerInAgroArea = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area");
        farmerInAgroArea.Headers.Add("X-User-Id", farmerId.ToString());
        var res2 = await _client.SendAsync(farmerInAgroArea);
        res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Agronomist accessing agronomist-area -> 200 OK
        using var agroInAgroArea = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area");
        agroInAgroArea.Headers.Add("X-User-Id", agronomistId.ToString());
        var res3 = await _client.SendAsync(agroInAgroArea);
        res3.StatusCode.Should().Be(HttpStatusCode.OK);

        // Agronomist accessing farmer-area -> 403 Forbidden
        using var agroInFarmerArea = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/farmer-area");
        agroInFarmerArea.Headers.Add("X-User-Id", agronomistId.ToString());
        var res4 = await _client.SendAsync(agroInFarmerArea);
        res4.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
