using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.Services;

namespace TarlaAsistani.UnitTests.Features.Weather;

[Trait("Category", "Weather")]
public class WeatherRiskEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenSubZeroTemperatureWithin24Hours_ShouldReturnCriticalFrostRisk()
    {
        // Arrange
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<WeatherPoint>
        {
            new(now.AddHours(2), 5.0, 0, 0, 10),
            new(now.AddHours(6), -2.0, 0, 0, 8), // Frost!
            new(now.AddHours(10), 4.0, 0, 0, 12)
        };

        // Act
        var risks = WeatherRiskEvaluator.Evaluate(points, now);

        // Assert
        risks.Should().ContainSingle(r => r.RiskType == "FROST");
        var frostRisk = risks.First(r => r.RiskType == "FROST");
        frostRisk.Severity.Should().Be("CRITICAL");
        frostRisk.Message.Should().Contain("don riski");
    }

    [Fact]
    public void Evaluate_WhenStrongWindWithin24Hours_ShouldReturnHighWindRisk()
    {
        // Arrange
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<WeatherPoint>
        {
            new(now.AddHours(3), 15.0, 0, 0, 35.0) // 35 km/h >= 30 km/h
        };

        // Act
        var risks = WeatherRiskEvaluator.Evaluate(points, now);

        // Assert
        risks.Should().ContainSingle(r => r.RiskType == "STRONG_WIND");
        var windRisk = risks.First(r => r.RiskType == "STRONG_WIND");
        windRisk.Severity.Should().Be("HIGH");
    }

    [Fact]
    public void Evaluate_WhenHeavyRainWithin12Hours_ShouldReturnHeavyRainRisk()
    {
        // Arrange
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<WeatherPoint>
        {
            new(now.AddHours(4), 12.0, 85.0, 8.0, 15.0) // 85% prob, 8mm/h
        };

        // Act
        var risks = WeatherRiskEvaluator.Evaluate(points, now);

        // Assert
        risks.Should().ContainSingle(r => r.RiskType == "HEAVY_RAIN");
        var rainRisk = risks.First(r => r.RiskType == "HEAVY_RAIN");
        rainRisk.Severity.Should().Be("HIGH");
    }

    [Fact]
    public void Evaluate_WhenNormalConditions_ShouldReturnNoRisks()
    {
        // Arrange
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<WeatherPoint>
        {
            new(now.AddHours(2), 18.0, 10.0, 0.0, 12.0),
            new(now.AddHours(5), 20.0, 5.0, 0.0, 10.0)
        };

        // Act
        var risks = WeatherRiskEvaluator.Evaluate(points, now);

        // Assert
        risks.Should().BeEmpty();
    }
}
