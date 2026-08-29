using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Services;

[Trait("Category", "Media")]
public class R2MediaStorageServiceTests
{
    private readonly Mock<ILogger<R2MediaStorageService>> _loggerMock = new();

    private IConfiguration CreateConfig()
    {
        var settings = new Dictionary<string, string?>
        {
            ["R2:AccountId"] = "fake-account",
            ["R2:Bucket"] = "tarla-media",
            ["R2:AccessKeyId"] = "fake-key",
            ["R2:SecretAccessKey"] = "fake-secret"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public async Task SaveAsync_ShouldSendPutRequestWithSigV4()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new R2MediaStorageService(client, CreateConfig(), _loggerMock.Object);
        var data = new byte[] { 1, 2, 3, 4 };

        // Act
        await service.SaveAsync("cases/leaf.jpg", data, "image/jpeg");

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Put &&
                req.RequestUri!.ToString().Contains("fake-account.r2.cloudflarestorage.com/tarla-media/cases/leaf.jpg") &&
                req.Headers.Authorization != null &&
                req.Headers.Authorization.Scheme == "AWS4-HMAC-SHA256"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WhenNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        var client = new HttpClient(handlerMock.Object);
        var service = new R2MediaStorageService(client, CreateConfig(), _loggerMock.Object);

        // Act
        var act = () => service.LoadAsync("cases/nonexistent.jpg");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
