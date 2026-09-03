using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Commands;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class SendAIChatMessageCommandHandlerQuotaTests
{
    private readonly Mock<IAIChatProvider> _mockAiProvider = new();
    private readonly Mock<IAIContextService> _mockContextService = new();
    private readonly Mock<IAIQuotaService> _mockQuotaService = new();
    private readonly IConfiguration _config;
    private readonly Guid _userId = Guid.NewGuid();

    public SendAIChatMessageCommandHandlerQuotaTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "gemini",
                ["AI:GeminiModel"] = "gemini-1.5-flash"
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
    public async Task Handle_WhenQuotaExceeded_ShouldFastFailAndNotCallProvider()
    {
        _mockQuotaService
            .Setup(q => q.CheckQuotaAsync(_userId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExceededException("Günlük fotoğraf analizi kotanıza ulaştınız."));

        var handler = new SendAIChatMessageCommandHandler(
            _mockAiProvider.Object,
            _mockContextService.Object,
            _mockQuotaService.Object,
            _config);

        var command = new SendAIChatMessageCommand(
            UserId: _userId,
            Message: "Yapraktaki lekeler nedir?",
            PhotoBytes: new byte[] { 1, 2, 3 },
            PhotoContentType: "image/jpeg"
        );

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<QuotaExceededException>()
            .WithMessage("*fotoğraf analizi kotanıza*");

        _mockAiProvider.Verify(p => p.GenerateAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockQuotaService.Verify(q => q.RecordUsageAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUnderQuota_ShouldCallProviderRecordUsageAndAttachQuotaInfo()
    {
        _mockQuotaService
            .Setup(q => q.CheckQuotaAsync(_userId, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAiProvider
            .Setup(p => p.GenerateAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIChatResponseDto("AI tavsiyesi", "conv-123", 100, 50, 150, 0.000022m));

        var handler = new SendAIChatMessageCommandHandler(
            _mockAiProvider.Object,
            _mockContextService.Object,
            _mockQuotaService.Object,
            _config);

        var command = new SendAIChatMessageCommand(
            UserId: _userId,
            Message: "Buğday ne zaman sulanmalı?"
        );

        var response = await handler.Handle(command, CancellationToken.None);

        response.Reply.Should().Be("AI tavsiyesi");
        response.ConversationId.Should().Be("conv-123");
        response.PromptTokens.Should().Be(100);
        response.CompletionTokens.Should().Be(50);
        response.EstimatedCostUsd.Should().Be(0.000022m);
        response.QuotaInfo.Should().NotBeNull();
        response.QuotaInfo!.TextsRemainingToday.Should().Be(95);

        _mockQuotaService.Verify(q => q.RecordUsageAsync(
            _userId,
            "gemini",
            "gemini-1.5-flash",
            false,
            100,
            50,
            It.IsAny<long>(),
            0.000022m,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
