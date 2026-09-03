using FluentAssertions;
using TarlaAsistani.Application.Features.AI.Services;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class AICostCalculatorTests
{
    private readonly AICostCalculator _calculator = new();

    [Fact]
    public void CalculateCost_WithZeroTokens_ShouldReturnZero()
    {
        var cost = _calculator.CalculateCost("gemini", "gemini-2.5-flash", 0, 0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateCost_ForGemini_ShouldCalculateCorrectly()
    {
        // 1,000,000 prompt tokens = $0.075
        // 1,000,000 completion tokens = $0.30
        var cost = _calculator.CalculateCost("gemini", "gemini-2.5-flash", 1_000_000, 1_000_000);
        cost.Should().Be(0.375m);
    }

    [Fact]
    public void CalculateCost_ForDeepSeek_ShouldCalculateCorrectly()
    {
        // 1,000,000 prompt tokens = $0.14
        // 1,000,000 completion tokens = $0.28
        var cost = _calculator.CalculateCost("deepseek", "deepseek-chat", 1_000_000, 1_000_000);
        cost.Should().Be(0.42m);
    }

    [Fact]
    public void CalculateCost_ForUnknownOrLocalProvider_ShouldReturnZero()
    {
        var cost = _calculator.CalculateCost("local", "local", 500, 200);
        cost.Should().Be(0m);
    }
}
