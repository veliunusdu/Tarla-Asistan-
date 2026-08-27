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
    public async Task FullOtpAuthFlow_ShouldSucceed()
    {
        // 1. Request OTP
        var phone = "+905551112233";
        var requestOtpResponse = await _client.PostAsJsonAsync("/api/v1/auth/request-otp", new RequestOtpApiRequest(phone));
        requestOtpResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var otpResult = await requestOtpResponse.Content.ReadFromJsonAsync<RequestOtpResponseDto>();
        otpResult.Should().NotBeNull();
        otpResult!.DebugOtp.Should().NotBeNullOrEmpty();

        // 2. Verify OTP
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpApiRequest(phone, otpResult.DebugOtp!));
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResult = await verifyResponse.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        tokenResult.Should().NotBeNull();
        tokenResult!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResult.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResult.User.PhoneNumber.Should().Be(phone);

        // 3. Query /me
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Add("X-User-Id", tokenResult.User.Id.ToString());
        var meResponse = await _client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResult = await meResponse.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        userResult.Should().NotBeNull();
        userResult!.Id.Should().Be(tokenResult.User.Id);

        // 4. Refresh Token
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenApiRequest(tokenResult.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshedToken = await refreshResponse.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        refreshedToken.Should().NotBeNull();
        refreshedToken!.AccessToken.Should().NotBeNullOrEmpty();
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
        var response = await _client.PostAsJsonAsync("/api/v1/auth/firebase", new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        tokenResult.Should().NotBeNull();
        tokenResult!.User.PhoneNumber.Should().Be(phone);
        tokenResult.User.FirebaseUid.Should().Be(uid);
    }
}
