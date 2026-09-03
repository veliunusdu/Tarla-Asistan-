using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

public class AIChatStreamingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AIChatStreamingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        async IAsyncEnumerable<AIChatStreamChunkDto> FakeStream()
        {
            yield return new AIChatStreamChunkDto(Content: "Pamuk ", ConversationId: "conv-stream-test");
            yield return new AIChatStreamChunkDto(Content: "sulaması ", ConversationId: "conv-stream-test");
            yield return new AIChatStreamChunkDto(Content: "önemlidir.", ConversationId: "conv-stream-test");
            yield return new AIChatStreamChunkDto(
                Done: true,
                ConversationId: "conv-stream-test",
                PromptTokens: 50,
                CompletionTokens: 25,
                TotalTokens: 75,
                EstimatedCostUsd: 0.00001m);
        }

        _factory.MockAIChatProvider
            .Setup(p => p.GenerateStreamAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()))
            .Returns(FakeStream());
    }

    private async Task SeedUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                PhoneNumber = $"+90554{Random.Shared.Next(1000000, 9999999)}",
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PostChatStream_ShouldReturnSseStreamWithDoneAndUsage()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var payload = new { message = "Pamuk nasıl sulanır?" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat/stream")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-User-Id", userId.ToString());

        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("data: {\"content\":\"Pamuk \"");
        body.Should().Contain("data: {\"content\":\"sulaması \"");
        body.Should().Contain("data: {\"content\":\"önemlidir.\"");
        body.Should().Contain("\"done\":true");
        body.Should().Contain("data: [DONE]");

        // Verify DB usage log persisted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = db.AiUsageLogs.FirstOrDefault(l => l.UserId == userId);
        log.Should().NotBeNull();
        log!.TotalTokens.Should().Be(75);
    }

    [Fact]
    public async Task PostChatStream_WhenQuotaExceeded_ShouldReturn429()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        // Exhaust photo quota
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (int i = 0; i < 5; i++)
            {
                db.AiUsageLogs.Add(new AiUsageLog
                {
                    UserId = userId,
                    Provider = "gemini",
                    Model = "gemini-2.5-flash",
                    HasPhoto = true,
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        var form = new MultipartFormDataContent();
        form.Add(new StringContent("Bu yaprak nedir?"), "message");
        var photoContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        photoContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(photoContent, "photo", "leaf.jpg");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat/stream")
        {
            Content = form
        };
        request.Headers.Add("X-User-Id", userId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("fotoğraf analizi kotanıza");
    }
}
