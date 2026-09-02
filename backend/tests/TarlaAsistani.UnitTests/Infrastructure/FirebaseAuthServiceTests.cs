using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Infrastructure;

public class FirebaseAuthServiceTests
{
    [Fact]
    public async Task VerifyIdTokenAsync_WhenProductionUsesDevelopmentToken_ReturnsNull()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Production);
        var service = new FirebaseAuthService(
            new Mock<ILogger<FirebaseAuthService>>().Object,
            environment.Object);

        var result = await service.VerifyIdTokenAsync("dev_test_user");

        result.Should().BeNull();
    }
}
