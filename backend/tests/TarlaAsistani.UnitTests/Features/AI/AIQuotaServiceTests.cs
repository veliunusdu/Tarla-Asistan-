using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class AIQuotaServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private IConfiguration CreateConfig(int photoLimit = 5, int textLimit = 100)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Quotas:DailyPhotoLimit"] = photoLimit.ToString(),
                ["AI:Quotas:DailyTextMessageLimit"] = textLimit.ToString()
            })
            .Build();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUnderLimits_ShouldNotThrow()
    {
        var db = new MockDbContextBuilder().Build();
        var service = new AIQuotaService(db, CreateConfig(5, 100), NullLogger<AIQuotaService>.Instance);

        var photoAct = () => service.CheckQuotaAsync(_userId, hasPhoto: true);
        var textAct = () => service.CheckQuotaAsync(_userId, hasPhoto: false);

        await photoAct.Should().NotThrowAsync();
        await textAct.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenPhotoLimitReached_ShouldThrowQuotaExceededException()
    {
        var logs = Enumerable.Range(0, 5).Select(_ => new AiUsageLog
        {
            UserId = _userId,
            HasPhoto = true,
            CreatedAtUtc = DateTime.UtcNow
        }).ToArray();

        var db = new MockDbContextBuilder().WithAiUsageLogs(logs).Build();
        var service = new AIQuotaService(db, CreateConfig(5, 100), NullLogger<AIQuotaService>.Instance);

        var act = () => service.CheckQuotaAsync(_userId, hasPhoto: true);

        await act.Should().ThrowAsync<QuotaExceededException>()
            .WithMessage("*fotoğraf analizi kotanıza*");
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenTextLimitReached_ShouldThrowQuotaExceededException()
    {
        var logs = Enumerable.Range(0, 100).Select(_ => new AiUsageLog
        {
            UserId = _userId,
            HasPhoto = false,
            CreatedAtUtc = DateTime.UtcNow
        }).ToArray();

        var db = new MockDbContextBuilder().WithAiUsageLogs(logs).Build();
        var service = new AIQuotaService(db, CreateConfig(5, 100), NullLogger<AIQuotaService>.Instance);

        var act = () => service.CheckQuotaAsync(_userId, hasPhoto: false);

        await act.Should().ThrowAsync<QuotaExceededException>()
            .WithMessage("*soru sorma kotanıza*");
    }

    [Fact]
    public async Task CheckQuotaAsync_LogsFromPreviousDays_ShouldNotCountTowardsToday()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var logs = Enumerable.Range(0, 10).Select(_ => new AiUsageLog
        {
            UserId = _userId,
            HasPhoto = true,
            CreatedAtUtc = yesterday
        }).ToArray();

        var db = new MockDbContextBuilder().WithAiUsageLogs(logs).Build();
        var service = new AIQuotaService(db, CreateConfig(5, 100), NullLogger<AIQuotaService>.Instance);

        var act = () => service.CheckQuotaAsync(_userId, hasPhoto: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetQuotaStatusAsync_ShouldCalculateRemainingCorrectly()
    {
        var logs = new[]
        {
            new AiUsageLog { UserId = _userId, HasPhoto = true, CreatedAtUtc = DateTime.UtcNow },
            new AiUsageLog { UserId = _userId, HasPhoto = true, CreatedAtUtc = DateTime.UtcNow },
            new AiUsageLog { UserId = _userId, HasPhoto = false, CreatedAtUtc = DateTime.UtcNow }
        };

        var db = new MockDbContextBuilder().WithAiUsageLogs(logs).Build();
        var service = new AIQuotaService(db, CreateConfig(5, 100), NullLogger<AIQuotaService>.Instance);

        var status = await service.GetQuotaStatusAsync(_userId);

        status.DailyPhotoLimit.Should().Be(5);
        status.PhotosUsedToday.Should().Be(2);
        status.PhotosRemainingToday.Should().Be(3);

        status.DailyTextLimit.Should().Be(100);
        status.TextsUsedToday.Should().Be(1);
        status.TextsRemainingToday.Should().Be(99);
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldPersistLogToDatabase()
    {
        var db = new MockDbContextBuilder().Build();
        var service = new AIQuotaService(db, CreateConfig(), NullLogger<AIQuotaService>.Instance);

        await service.RecordUsageAsync(
            userId: _userId,
            provider: "gemini",
            model: "gemini-2.5-flash",
            hasPhoto: true,
            promptTokens: 150,
            completionTokens: 80,
            durationMs: 420,
            estimatedCostUsd: 0.000035m);

        var status = await service.GetQuotaStatusAsync(_userId);
        status.PhotosUsedToday.Should().Be(1);
        status.PhotosRemainingToday.Should().Be(4);
    }
}
