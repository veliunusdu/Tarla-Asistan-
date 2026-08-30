using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class DeepSeekAIChatProviderTests
{
    [Fact]
    public async Task GenerateAsync_WhenRootEnvironmentStyleKeyIsConfigured_ShouldCallDeepSeek()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "Gerçek DeepSeek yanıtı" } } } }),
                    Encoding.UTF8,
                    "application/json")
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DEEPSEEK_API_KEY"] = "render-deepseek-key",
                ["DEEPSEEK_MODEL"] = "deepseek-chat",
                ["DEEPSEEK_BASE_URL"] = "https://api.deepseek.com"
            })
            .Build();
        var provider = new DeepSeekAIChatProvider(new HttpClient(handlerMock.Object), config);

        var result = await provider.GenerateAsync(new AIChatRequestDto("Mısır yaprakları sararıyor", null, null, null));

        result.Reply.Should().Be("Gerçek DeepSeek yanıtı");
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Headers.Authorization != null &&
                request.Headers.Authorization.Scheme == "Bearer" &&
                request.Headers.Authorization.Parameter == "render-deepseek-key"),
            ItExpr.IsAny<CancellationToken>());
    }
}
