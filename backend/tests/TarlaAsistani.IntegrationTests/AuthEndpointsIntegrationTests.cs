using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class AuthEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FirebaseLogin_WhenValidToken_ShouldCreateAndAuthenticateUser()
    {
        // Arrange
        var firebaseToken = "firebase_test_token_123";
        var uid = "f_uid_98765";
        var phone = "+905553334455";

        _factory.MockFirebaseAuthService
            .Setup(f => f.VerifyIdTokenAsync(firebaseToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "farmer@test.com", "Hasan Çiftçi"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer), CustomWebApplicationFactory.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        tokenResult.Should().NotBeNull();
        tokenResult!.User.PhoneNumber.Should().Be(phone);
        tokenResult.User.FirebaseUid.Should().Be(uid);
    }

    [Fact]
    public async Task JwtBearerAuth_WhenValidBearerToken_ShouldResolveUserOnMeEndpoint()
    {
        // Arrange: Exchange a verified Firebase token for a backend JWT.
        const string firebaseToken = "firebase_bearer_test_token";
        const string uid = "firebase_bearer_test_uid";
        const string phone = "+905557778899";
        _factory.MockFirebaseAuthService
            .Setup(f => f.VerifyIdTokenAsync(firebaseToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "bearer@test.com", "Bearer Test Farmer"));

        var firebaseResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        firebaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResult = await firebaseResponse.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var accessToken = tokenResult!.AccessToken;

        // Act: Call /me using ONLY Authorization: Bearer <token> (WITHOUT X-User-Id header)
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        var meResponse = await _client.SendAsync(meRequest);

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userResult = await meResponse.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        userResult.Should().NotBeNull();
        userResult!.Id.Should().Be(tokenResult.User.Id);
        userResult.PhoneNumber.Should().Be(phone);
    }
}
