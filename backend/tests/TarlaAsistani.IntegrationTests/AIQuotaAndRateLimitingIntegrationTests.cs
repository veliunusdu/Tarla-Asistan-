using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public class AIQuotaAndRateLimitingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AIQuotaAndRateLimitingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Setup mock AI provider with token usage
        _factory.MockAIChatProvider
            .Setup(p => p.GenerateAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIChatRequestDto req, CancellationToken _) =>
                new AIChatResponseDto("AI Yanıtı", req.ConversationId ?? Guid.NewGuid().ToString("N"), 120, 60, 180, 0.000027m));
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
                PhoneNumber = $"+90555{Random.Shared.Next(1000000, 9999999)}",
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetQuota_ShouldReturnInitialQuota()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ai/quota");
        request.Headers.Add("X-User-Id", userId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quota = await response.Content.ReadFromJsonAsync<AIQuotaStatusDto>(CustomWebApplicationFactory.JsonOptions);

        quota.Should().NotBeNull();
        quota!.DailyPhotoLimit.Should().Be(5);
        quota.PhotosRemainingToday.Should().Be(5);
        quota.DailyTextLimit.Should().Be(100);
        quota.TextsRemainingToday.Should().Be(100);
    }

    [Fact]
    public async Task PostChat_TextQuery_ShouldDecrementTextQuotaAndReturnHeaders()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var payload = new { message = "Buğday ne zaman hasat edilir?" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-User-Id", userId.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Quota-Text-Remaining");

        var result = await response.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        result.Should().NotBeNull();
        result!.Reply.Should().Be("AI Yanıtı");
        result.QuotaInfo.Should().NotBeNull();
        result.QuotaInfo!.TextsRemainingToday.Should().Be(99);

        // Verify DB logging
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = db.AiUsageLogs.FirstOrDefault(l => l.UserId == userId);
        log.Should().NotBeNull();
        log!.HasPhoto.Should().BeFalse();
        log.PromptTokens.Should().Be(120);
        log.CompletionTokens.Should().Be(60);
    }

    [Fact]
    public async Task PostChat_WhenPhotoQuotaExceeded_ShouldReturn429()
    {
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        // Seed 5 existing photo logs for today to exhaust photo quota
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

        // Attempt 6th photo request via multipart
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("Bu yaprakta ne var?"), "message");
        var photoContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        photoContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(photoContent, "photo", "leaf.jpg");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
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
