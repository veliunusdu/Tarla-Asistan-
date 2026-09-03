using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Commands;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class StreamAIChatMessageCommandHandlerTests
{
    private readonly Mock<IAIChatProvider> _mockAiProvider = new();
    private readonly Mock<IAIContextService> _mockContextService = new();
    private readonly Mock<IAIQuotaService> _mockQuotaService = new();
    private readonly IConfiguration _config;
    private readonly Guid _userId = Guid.NewGuid();

    public StreamAIChatMessageCommandHandlerTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "gemini",
                ["AI:GeminiModel"] = "gemini-2.5-flash"
            })
            .Build();

        _mockContextService
            .Setup(c => c.BuildContextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIAccountContext("Ahmet", new List<AIFarmSummary>()));

        _mockQuotaService
            .Setup(q => q.GetQuotaStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIQuotaStatusDto(5, 1, 4, 100, 5, 95, DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task Handle_WhenQuotaExceeded_ShouldThrowQuotaExceededException()
    {
        _mockQuotaService
            .Setup(q => q.CheckQuotaAsync(_userId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExceededException("Günlük soru sorma kotanıza ulaştınız."));

        var handler = new StreamAIChatMessageCommandHandler(
            _mockAiProvider.Object,
            _mockContextService.Object,
            _mockQuotaService.Object,
            _config);

        var command = new StreamAIChatMessageCommand(
            UserId: _userId,
            Message: "Mısır ne zaman ekilir?"
        );

        var act = async () =>
        {
            await foreach (var _ in handler.Handle(command, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<QuotaExceededException>()
            .WithMessage("*soru sorma kotanıza*");

        _mockAiProvider.Verify(p => p.GenerateStreamAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockQuotaService.Verify(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUnderQuota_ShouldYieldChunksAndRecordUsage()
    {
        _mockQuotaService
            .Setup(q => q.CheckQuotaAsync(_userId, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        async IAsyncEnumerable<AIChatStreamChunkDto> FakeStream()
        {
            yield return new AIChatStreamChunkDto(Content: "Mısır ", ConversationId: "conv-stream-1");
            yield return new AIChatStreamChunkDto(Content: "ekimi ", ConversationId: "conv-stream-1");
            yield return new AIChatStreamChunkDto(Content: "Mayıs'ta yapılır.", ConversationId: "conv-stream-1");
            yield return new AIChatStreamChunkDto(
                Done: true,
                ConversationId: "conv-stream-1",
                PromptTokens: 80,
                CompletionTokens: 30,
                TotalTokens: 110,
                EstimatedCostUsd: 0.000015m);
        }

        _mockAiProvider
            .Setup(p => p.GenerateStreamAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()))
            .Returns(FakeStream());

        var handler = new StreamAIChatMessageCommandHandler(
            _mockAiProvider.Object,
            _mockContextService.Object,
            _mockQuotaService.Object,
            _config);

        var command = new StreamAIChatMessageCommand(
            UserId: _userId,
            Message: "Mısır ne zaman ekilir?"
        );

        var received = new List<AIChatStreamChunkDto>();
        await foreach (var chunk in handler.Handle(command, CancellationToken.None))
        {
            received.Add(chunk);
        }

        // Verify content chunks yielded
        received.Should().HaveCount(4); // 3 content + 1 final done
        received[0].Content.Should().Be("Mısır ");
        received[1].Content.Should().Be("ekimi ");
        received[2].Content.Should().Be("Mayıs'ta yapılır.");

        // Verify final chunk
        var finalChunk = received.Last();
        finalChunk.Done.Should().BeTrue();
        finalChunk.ConversationId.Should().Be("conv-stream-1");
        finalChunk.TotalTokens.Should().Be(110);
        finalChunk.EstimatedCostUsd.Should().Be(0.000015m);
        finalChunk.QuotaInfo.Should().NotBeNull();
        finalChunk.QuotaInfo!.TextsRemainingToday.Should().Be(95);

        // Verify RecordUsage was called
        _mockQuotaService.Verify(q => q.RecordUsageAsync(
            _userId,
            "gemini",
            "gemini-2.5-flash",
            false,
            80,
            30,
            It.IsAny<long>(),
            0.000015m,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
