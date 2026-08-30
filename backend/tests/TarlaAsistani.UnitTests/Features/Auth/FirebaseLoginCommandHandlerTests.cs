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

    [Fact]
    public async Task Handle_WhenAgronomistWithoutApprovalAttemptsToLink_ShouldThrowUnauthorized()
    {
        // Arrange
        var idToken = "firebase_agronomist_token";
        var uid = "agro_uid_123";
        var phone = "+905554443322";

        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "agro@example.com", "Ayşe Uzman"));

        var agronomist = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            FirebaseUid = null,
            Role = UserRole.Agronomist,
            AccountStatus = AccountStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist)
            .Build();

        var config = CreateConfig();
        var handler = new FirebaseLoginCommandHandler(db, _firebaseAuthMock.Object, _jwtServiceMock.Object, config);
        var command = new FirebaseLoginCommand(idToken);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*onay*");
    }

    [Fact]
    public async Task Handle_WhenAgronomistWithValidApprovalAttemptsToLink_ShouldConsumeApprovalAndLinkUid()
    {
        // Arrange
        var idToken = "firebase_agronomist_token";
        var uid = "agro_uid_123";
        var phone = "+905554443322";

        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "agro@example.com", "Ayşe Uzman"));

        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("fake_jwt");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("fake_refresh");

        var agronomist = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            FirebaseUid = null,
            Role = UserRole.Agronomist,
            AccountStatus = AccountStatus.Active
        };

        var approval = new FirebaseLinkApproval
        {
            Id = Guid.NewGuid(),
            UserId = agronomist.Id,
            FirebaseUid = uid,
            ApprovedBy = "admin@tarla.local",
            ApprovedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            ConsumedAtUtc = null
        };

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist)
            .WithFirebaseLinkApprovals(approval)
            .Build();

        var config = CreateConfig();
        var handler = new FirebaseLoginCommandHandler(db, _firebaseAuthMock.Object, _jwtServiceMock.Object, config);
        var command = new FirebaseLoginCommand(idToken);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.FirebaseUid.Should().Be(uid);
        approval.ConsumedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenNewUserSelfRegisters_ShouldAlwaysDefaultToFarmerRole()
    {
        // Arrange
        var idToken = "firebase_new_user_token";
        var uid = "new_uid_999";
        var phone = "+905550001122";

        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, "new@example.com", "Yeni Kullanıcı"));

        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("fake_jwt");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("fake_refresh");

        var db = new MockDbContextBuilder().Build();
        var config = CreateConfig();
        var handler = new FirebaseLoginCommandHandler(db, _firebaseAuthMock.Object, _jwtServiceMock.Object, config);

        // Attempt to pass Agronomist role
        var command = new FirebaseLoginCommand(idToken, UserRole.Agronomist);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - new self-registered users MUST always be created as Farmer
        result.User.Role.Should().Be(UserRole.Farmer);
    }

    [Fact]
    public async Task Handle_WhenEmailOnlyFirebaseUsersRegister_ShouldAssignDistinctInternalPhoneIdentifiers()
    {
        const string firstToken = "firebase_email_user_1";
        const string secondToken = "firebase_email_user_2";

        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(firstToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo("email_uid_1", null, "first@example.com", null));
        _firebaseAuthMock.Setup(f => f.VerifyIdTokenAsync(secondToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo("email_uid_2", null, "second@example.com", null));
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("fake_jwt");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("fake_refresh");

        var handler = new FirebaseLoginCommandHandler(
            new MockDbContextBuilder().Build(),
            _firebaseAuthMock.Object,
            _jwtServiceMock.Object,
            CreateConfig());

        var first = await handler.Handle(new FirebaseLoginCommand(firstToken), CancellationToken.None);
        var second = await handler.Handle(new FirebaseLoginCommand(secondToken), CancellationToken.None);

        first.User.PhoneNumber.Should().StartWith("firebase-");
        second.User.PhoneNumber.Should().StartWith("firebase-");
        second.User.PhoneNumber.Should().NotBe(first.User.PhoneNumber);
    }
}
