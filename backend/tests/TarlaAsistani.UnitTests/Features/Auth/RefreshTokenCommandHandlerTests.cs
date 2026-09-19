using System.Security.Cryptography;
using System.Text;
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
public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IJwtService> _jwtServiceMock = new();

    private IConfiguration CreateConfig()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Auth:RefreshTokenExpireDays"] = "30",
            ["Auth:AccessTokenExpireMinutes"] = "15"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    [Fact]
    public async Task Handle_WhenValidRefreshToken_ShouldRotateAndPreserveActiveRole()
    {
        // Arrange
        const string rawOldToken = "valid_old_refresh_token";
        const string rawNewToken = "fresh_rotated_refresh_token";
        var oldHash = HashToken(rawOldToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905551112233",
            FirebaseUid = "test_uid",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };

        var farmerRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = UserRole.Farmer,
            GrantedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        var agronomistRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = UserRole.Agronomist,
            GrantedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        var familyId = Guid.NewGuid();
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = oldHash,
            ActiveRole = UserRole.Agronomist,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithUserRoleAssignments(farmerRole, agronomistRole)
            .WithRefreshTokens(storedToken)
            .Build();

        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns(rawNewToken);
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(user, UserRole.Agronomist)).Returns("new_access_token");

        var config = CreateConfig();
        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new RefreshTokenCommand(rawOldToken);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new_access_token");
        result.RefreshToken.Should().Be(rawNewToken);
        result.User.ActiveRole.Should().Be(UserRole.Agronomist);
        result.User.Roles.Should().Contain(new[] { UserRole.Farmer, UserRole.Agronomist });

        // Stored old token should now be revoked
        storedToken.RevokedAtUtc.Should().NotBeNull();

        // New replacement token must be added with the same FamilyId and ActiveRole
        var replacement = db.RefreshTokens.FirstOrDefault(r => r.TokenHash == HashToken(rawNewToken));
        replacement.Should().NotBeNull();
        replacement!.FamilyId.Should().Be(familyId);
        replacement.ActiveRole.Should().Be(UserRole.Agronomist);
        replacement.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenActiveRoleHasBeenRevoked_ShouldRevokeCurrentTokenAndThrowUnauthorized()
    {
        // Arrange
        const string rawToken = "valid_format_refresh_token";
        var oldHash = HashToken(rawToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905551112233",
            FirebaseUid = "test_uid",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };

        var farmerRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = UserRole.Farmer,
            GrantedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        // Agronomist role was granted earlier but has since been revoked
        var revokedAgronomistRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = UserRole.Agronomist,
            GrantedAtUtc = DateTime.UtcNow.AddDays(-5),
            RevokedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = Guid.NewGuid(),
            TokenHash = oldHash,
            ActiveRole = UserRole.Agronomist,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithUserRoleAssignments(farmerRole, revokedAgronomistRole)
            .WithRefreshTokens(storedToken)
            .Build();

        var config = CreateConfig();
        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new RefreshTokenCommand(rawToken);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*yetki geri alındı*");

        // The refresh token should immediately be marked as revoked
        storedToken.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenTokenExpired_ShouldRevokeAndThrowUnauthorized()
    {
        // Arrange
        const string rawToken = "expired_token";
        var hash = HashToken(rawToken);

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = hash,
            ActiveRole = UserRole.Farmer,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        var db = new MockDbContextBuilder()
            .WithRefreshTokens(storedToken)
            .Build();

        var config = CreateConfig();
        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new RefreshTokenCommand(rawToken);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*süresi doldu*");
        storedToken.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenTokenAlreadyRevoked_ShouldThrowUnauthorized()
    {
        // Arrange
        const string rawToken = "revoked_token";
        var hash = HashToken(rawToken);

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = hash,
            ActiveRole = UserRole.Farmer,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
            RevokedAtUtc = DateTime.UtcNow.AddHours(-2),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        var db = new MockDbContextBuilder()
            .WithRefreshTokens(storedToken)
            .Build();

        var config = CreateConfig();
        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new RefreshTokenCommand(rawToken);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*süresi doldu*");
    }
}
