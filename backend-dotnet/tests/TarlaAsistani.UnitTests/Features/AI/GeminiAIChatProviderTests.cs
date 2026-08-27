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
public class GeminiAIChatProviderTests
{
    [Fact]
    public async Task GenerateAsync_WhenApiKeyEmpty_ShouldFallbackToLocal()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["AI:GeminiApiKey"] = ""
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var client = new HttpClient();
        var provider = new GeminiAIChatProvider(client, config);

        var request = new AIChatRequestDto(
            Message: "Mısır yaprakları sararıyor",
            FieldId: null,
            ConversationId: null,
            History: null
        );

        // Act
        var result = await provider.GenerateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Reply.Should().Contain("AI sağlayıcısı bağlandığında");
    }

    [Fact]
    public async Task GenerateAsync_WithPhoto_ShouldSendInlineDataAndReturnReply()
    {
        // Arrange
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = "Bu fotoğrafta yaprak yanıklığı tespit edildi. Fungisit uygulaması önerilir." }
                        }
                    }
                }
            }
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            });

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["AI:GeminiApiKey"] = "fake-gemini-key",
            ["AI:GeminiModel"] = "gemini-2.0-flash"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var client = new HttpClient(handlerMock.Object);
        var provider = new GeminiAIChatProvider(client, config);

        var photoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // Dummy JPEG header
        var request = new AIChatRequestDto(
            Message: "Bu yapraktaki lekeler nedir?",
            FieldId: "field-1",
            ConversationId: "conv-1",
            History: null,
            PhotoBytes: photoBytes,
            PhotoContentType: "image/jpeg"
        );

        // Act
        var result = await provider.GenerateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Reply.Should().Contain("yaprak yanıklığı tespit edildi");
        result.ConversationId.Should().Be("conv-1");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("gemini-2.0-flash:generateContent") &&
                !req.RequestUri!.ToString().Contains("?key=") &&
                req.Headers.GetValues("x-goog-api-key").First() == "fake-gemini-key"),
            ItExpr.IsAny<CancellationToken>());
    }
}
