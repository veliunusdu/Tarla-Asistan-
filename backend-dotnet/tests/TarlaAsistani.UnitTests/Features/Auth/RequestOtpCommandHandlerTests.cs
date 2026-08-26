using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Application.Features.Auth.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Auth;

[Trait("Category", "Auth")]
public class RequestOtpCommandHandlerTests
{
    private readonly Mock<ILogger<RequestOtpCommandHandler>> _loggerMock = new();

    private IConfiguration CreateConfig(int cooldownSeconds = 60, int ttlSeconds = 180, string env = "local")
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Auth:OtpCooldownSeconds"] = cooldownSeconds.ToString(),
            ["Auth:OtpTtlSeconds"] = ttlSeconds.ToString(),
            ["Environment"] = env
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreateOtpAndReturnDebugOtpInLocalEnv()
    {
        // Arrange
        var db = new MockDbContextBuilder().Build();
        var config = CreateConfig();
        var handler = new RequestOtpCommandHandler(db, config, _loggerMock.Object);
        var command = new RequestOtpCommand("+905551234567");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ExpiresIn.Should().Be(180);
        result.DebugOtp.Should().NotBeNullOrWhiteSpace();
        result.DebugOtp!.Length.Should().Be(6);
    }

    [Fact]
    public async Task Handle_WhenOtpRequestedWithinCooldown_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var phone = "+905551234567";
        var existingOtp = new OtpCode
        {
            PhoneNumber = phone,
            CodeHash = "somehash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(3),
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(-20) // Requested 20s ago (within 60s cooldown)
        };

        var db = new MockDbContextBuilder()
            .WithOtpCodes(existingOtp)
            .Build();

        var config = CreateConfig(cooldownSeconds: 60);
        var handler = new RequestOtpCommandHandler(db, config, _loggerMock.Object);
        var command = new RequestOtpCommand(phone);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bekleyin*");
    }
}
