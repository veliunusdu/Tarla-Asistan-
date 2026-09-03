using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;

namespace TarlaAsistani.UnitTests.Features.AI;

public class AIAgentSystemPromptBuilderTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public void Build_WithFixedTimeAndIstanbulTimeZone_ContainsRequiredInvariants()
    {
        // Arrange
        // Fixed UTC: 2026-09-03 19:00:00 UTC = 2026-09-03 22:00:00 UTC+3 (Istanbul)
        var fakeTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:TimeZone"] = "Europe/Istanbul"
            })
            .Build();

        var builder = new AIAgentSystemPromptBuilder(fakeTimeProvider, config);

        var farmId = Guid.NewGuid();
        var context = new AIAccountContext(
            DisplayName: "Ahmet Çiftçi",
            Farms: new List<AIFarmSummary>
            {
                new(
                    FarmId: farmId,
                    Name: "Kuzey Tarlası",
                    CurrentCrop: "Buğday",
                    AreaHa: 12.5,
                    NextTask: null,
                    NextTaskDueDate: null,
                    LastActivity: null,
                    LastActivityAt: null,
                    Weather: null)
            });

        // Act
        var prompt = builder.Build(context);

        // Assert
        // Current local date & timezone
        prompt.Should().Contain("Mevcut yerel tarih: 2026-09-03");
        prompt.Should().Contain("Europe/Istanbul");

        // Tool discipline
        prompt.Should().Contain("list_farms");
        prompt.Should().Contain("get_weather");
        prompt.Should().Contain("get_tasks");
        prompt.Should().Contain("create_task");
        prompt.Should().Contain("ASLA tarla ID'si, görev ID'si, hava durumu değerleri");
        prompt.Should().Contain("duplicate_task");

        // Relative date rule
        prompt.Should().Contain("ISO formatındaki tarihi (YYYY-MM-DD)");
        prompt.Should().Contain("2026-09-04");

        // Time-of-day limitation
        prompt.Should().Contain("takvim günü (YYYY-MM-DD)");
        prompt.Should().Contain("saat veya günün vakti");

        // Trust boundary
        prompt.Should().Contain("Kimlik doğrulama ve yetkilendirme sunucu tarafında uygulanır");

        // Farm overview context
        prompt.Should().Contain("Kuzey Tarlası");
        prompt.Should().Contain("Buğday");
        prompt.Should().Contain("Ahmet Çiftçi");

        // Must NOT mention provider-specific terms
        prompt.Should().NotContain("Gemini");
        prompt.Should().NotContain("DeepSeek");
        prompt.Should().NotContain("functionCall");
        prompt.Should().NotContain("tool_calls");
    }

    [Fact]
    public void Build_WithoutAccountContext_BuildsSafePrompt()
    {
        // Arrange
        var fakeTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));

        var builder = new AIAgentSystemPromptBuilder(fakeTimeProvider);

        // Act
        var prompt = builder.Build(null);

        // Assert
        prompt.Should().NotBeNullOrWhiteSpace();
        prompt.Should().Contain("Mevcut yerel tarih: 2026-09-03");
        prompt.Should().Contain("Kullanıcının henüz kayıtlı bir tarlası görünmüyor");
    }
}
