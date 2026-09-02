using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class FarmWeatherContextTests
{
    [Fact]
    public void ToAiContext_ShouldExtractNormalizedSummaryCorrectly()
    {
        var farmId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var points = new List<WeatherPoint>
        {
            new(now, 24.5, 10, 0.0, 15.0, 65.0, 2),
            new(now.AddHours(1), 23.0, 80, 2.5, 20.0, 70.0, 61),
            new(now.AddHours(2), 20.0, 50, 1.0, 18.0, 80.0, 61),
        };

        var current = new CurrentWeatherDto(
            ObservedAt: now,
            TemperatureC: 24.5,
            FeelsLikeC: 25.0,
            HumidityPercent: 65.0,
            WindSpeedKmh: 15.0,
            WindGustsKmh: 25.0,
            Condition: "Parçalı Bulutlu",
            WeatherCode: 2
        );

        var risks = new List<WeatherRiskDto>
        {
            new("RAIN", "WARNING", now, now.AddHours(3), "Yağış bekleniyor", "İlaçlama yapmayın")
        };

        var dto = new FarmWeatherResponseDto(
            FarmId: farmId,
            Provider: "open_meteo",
            FetchedAt: now,
            IsStale: false,
            StaleReason: null,
            Points: points,
            Risks: risks,
            Current: current,
            Daily: null
        );

        var aiContext = dto.ToAiContext();

        aiContext.FarmId.Should().Be(farmId);
        aiContext.CurrentTemperatureC.Should().Be(24.5);
        aiContext.CurrentFeelsLikeC.Should().Be(25.0);
        aiContext.CurrentHumidityPercent.Should().Be(65.0);
        aiContext.CurrentWindSpeedKmh.Should().Be(15.0);
        aiContext.Condition.Should().Be("Parçalı Bulutlu");
        aiContext.WeatherCode.Should().Be(2);
        aiContext.NextRainProbability.Should().Be(80);
        aiContext.Next24HoursPrecipitationMm.Should().Be(3.5);
        aiContext.IsStale.Should().BeFalse();
        aiContext.ActiveRisks.Should().HaveCount(1);
    }
}
