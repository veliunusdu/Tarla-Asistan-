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
public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IJwtService> _jwtServiceMock = new();

    private IConfiguration CreateConfig(int maxAttempts = 3, int refreshTokenDays = 30)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Auth:OtpMaxAttempts"] = maxAttempts.ToString(),
            ["Auth:RefreshTokenExpiryDays"] = refreshTokenDays.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task Handle_WhenValidCode_ShouldAutoCreateUserAndReturnTokens()
    {
        // Arrange
        var phone = "+905551234567";
        var code = "123456";
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var otp = new OtpCode
        {
            PhoneNumber = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(3),
            IsUsed = false,
            AttemptCount = 0
        };

        var db = new MockDbContextBuilder()
            .WithOtpCodes(otp)
            .Build();

        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("fake_access_token");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken())
            .Returns("fake_refresh_token");

        var config = CreateConfig();
        var handler = new VerifyOtpCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new VerifyOtpCommand(phone, code);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("fake_access_token");
        result.RefreshToken.Should().Be("fake_refresh_token");
        result.User.PhoneNumber.Should().Be(phone);
        result.User.Role.Should().Be(UserRole.Farmer);
        otp.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenInvalidCode_ShouldIncrementAttemptCountAndThrow()
    {
        // Arrange
        var phone = "+905551234567";
        var correctCode = "123456";
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(correctCode)));

        var otp = new OtpCode
        {
            PhoneNumber = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(3),
            IsUsed = false,
            AttemptCount = 0
        };

        var db = new MockDbContextBuilder()
            .WithOtpCodes(otp)
            .Build();

        var config = CreateConfig();
        var handler = new VerifyOtpCommandHandler(db, _jwtServiceMock.Object, config);
        var command = new VerifyOtpCommand(phone, "999999"); // Wrong code

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*geçersiz*");
        otp.AttemptCount.Should().Be(1);
    }
}
