using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Auth;

[Trait("Category", "Auth")]
public class FirebaseLoginCommandHandlerTests
{
    private readonly Mock<IFirebaseAuthService> _firebaseAuthMock = new();
    private readonly Mock<IJwtService> _jwtServiceMock = new();

    private IConfiguration CreateConfig()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Auth:RefreshTokenExpiryDays"] = "30"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public async Task Handle_WhenValidFirebaseToken_ShouldAutoCreateUserAndReturnJwtTokens()
    {
        // Arrange
        var idToken = "valid_firebase_id_token";
        var uid = "firebase_uid_12345";
        var phone = "+905559876543";

        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "test@example.com", "Mehmet Demir"));

        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("fake_access_token");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken())
            .Returns("fake_refresh_token");

        var db = new MockDbContextBuilder().Build();
        var config = CreateConfig();
        var handler = new FirebaseLoginCommandHandler(db, _firebaseAuthMock.Object, _jwtServiceMock.Object, config);
        var command = new FirebaseLoginCommand(idToken, UserRole.Farmer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("fake_access_token");
        result.RefreshToken.Should().Be("fake_refresh_token");
        result.User.FirebaseUid.Should().Be(uid);
        result.User.PhoneNumber.Should().Be(phone);
        result.User.FullName.Should().Be("Mehmet Demir");
    }

    [Fact]
    public async Task Handle_WhenInvalidToken_ShouldThrowUnauthorized()
    {
        // Arrange
        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FirebaseTokenInfo?)null);

        var db = new MockDbContextBuilder().Build();
        var config = CreateConfig();
        var handler = new FirebaseLoginCommandHandler(db, _firebaseAuthMock.Object, _jwtServiceMock.Object, config);
        var command = new FirebaseLoginCommand("invalid_token");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*belirteci*");
    }
}
