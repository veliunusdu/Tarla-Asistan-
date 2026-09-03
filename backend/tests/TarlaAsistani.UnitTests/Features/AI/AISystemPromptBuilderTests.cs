using FluentAssertions;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class AISystemPromptBuilderTests
{
    [Fact]
    public void Build_WithNullContext_ShouldContainNoFarmMessage()
    {
        var prompt = AISystemPromptBuilder.Build(null);

        prompt.Should().Contain("tarla");
        prompt.Should().Contain("ziraat");
    }

    [Fact]
    public void Build_WithFarmAndCrop_ShouldContainFarmNameAndCrop()
    {
        var ctx = new AIAccountContext(
            DisplayName: "Ali Yılmaz",
            Farms: new List<AIFarmSummary>
            {
                new AIFarmSummary(
                    FarmId: Guid.NewGuid(),
                    Name: "Kuzey Tarla",
                    CurrentCrop: "Wheat",
                    AreaHa: 5.0,
                    NextTask: "İlaçlama",
                    NextTaskDueDate: new DateOnly(2026, 9, 5),
                    LastActivity: "Damla sulama",
                    LastActivityAt: DateTime.UtcNow.AddDays(-1),
                    Weather: null
                )
            });

        var prompt = AISystemPromptBuilder.Build(ctx);

        prompt.Should().Contain("Kuzey Tarla");
        prompt.Should().Contain("Wheat");
        prompt.Should().Contain("İlaçlama");
        prompt.Should().Contain("Damla sulama");
        prompt.Should().Contain("Ali Yılmaz");
    }

    [Fact]
    public void Build_WithWeatherContext_ShouldContainWeatherBlock()
    {
        var weather = new AIWeatherAiContext(
            FarmName: "Kuzey Tarla",
            CurrentTemperatureC: 23.5,
            HumidityPercent: 71.0,
            WindSpeedKmh: 14.0,
            Condition: "Parçalı Bulutlu",
            NextRainProbabilityPct: 78.0,
            Next24HoursPrecipitationMm: 8.0,
            IsStale: false,
            StaleReason: null,
            DataTime: DateTime.UtcNow,
            RiskSummaries: new List<string> { "STRONG_WIND: Kuvvetli rüzgar bekleniyor" }
        );

        var ctx = new AIAccountContext(
            DisplayName: null,
            Farms: new List<AIFarmSummary>
            {
                new AIFarmSummary(
                    FarmId: Guid.NewGuid(),
                    Name: "Kuzey Tarla",
                    CurrentCrop: "Buğday",
                    AreaHa: null,
                    NextTask: null,
                    NextTaskDueDate: null,
                    LastActivity: null,
                    LastActivityAt: null,
                    Weather: weather
                )
            });

        var prompt = AISystemPromptBuilder.Build(ctx);

        prompt.Should().Contain("23"); // temperature
        prompt.Should().Contain("71"); // humidity
        prompt.Should().Contain("14"); // wind
        prompt.Should().Contain("Parçalı Bulutlu");
        prompt.Should().Contain("STRONG_WIND");
        prompt.Should().Contain("güncel"); // freshness
    }

    [Fact]
    public void Build_WithStaleWeather_ShouldIndicateStale()
    {
        var weather = new AIWeatherAiContext(
            FarmName: "Eski Tarla",
            CurrentTemperatureC: 20.0,
            HumidityPercent: 60.0,
            WindSpeedKmh: 8.0,
            Condition: null,
            NextRainProbabilityPct: null,
            Next24HoursPrecipitationMm: null,
            IsStale: true,
            StaleReason: "Sağlayıcıya ulaşılamadı",
            DataTime: DateTime.UtcNow.AddHours(-6),
            RiskSummaries: new List<string>()
        );

        var ctx = new AIAccountContext(
            DisplayName: null,
            Farms: new List<AIFarmSummary>
            {
                new AIFarmSummary(
                    FarmId: Guid.NewGuid(),
                    Name: "Eski Tarla",
                    CurrentCrop: null,
                    AreaHa: null,
                    NextTask: null,
                    NextTaskDueDate: null,
                    LastActivity: null,
                    LastActivityAt: null,
                    Weather: weather
                )
            });

        var prompt = AISystemPromptBuilder.Build(ctx);

        prompt.Should().Contain("güncel değil", because: "stale weather should be marked as not current");
    }

    [Fact]
    public void Build_WhenRequestedWeatherIsUnavailable_ShouldSaySo()
    {
        var ctx = new AIAccountContext(
            DisplayName: null,
            Farms: new List<AIFarmSummary>
            {
                new(
                    FarmId: Guid.NewGuid(),
                    Name: "Kuzey Tarla",
                    CurrentCrop: null,
                    AreaHa: null,
                    NextTask: null,
                    NextTaskDueDate: null,
                    LastActivity: null,
                    LastActivityAt: null,
                    Weather: null,
                    WeatherRequested: true)
            });

        var prompt = AISystemPromptBuilder.Build(ctx);

        prompt.Should().Contain("HAVA — Kuzey Tarla: Mevcut değil");
    }

    [Fact]
    public void Build_ShouldAlwaysContainGroundRules()
    {
        var prompt = AISystemPromptBuilder.Build(null);

        // Core safety rules
        prompt.Should().Contain("ASLA uydurma");
        prompt.Should().Contain("kesinlik değildir");
        prompt.ToLower().Should().Contain("kimyasal doz");
    }

    [Fact]
    public void Build_WithHighWorkWeatherSignal_ShouldKeepTaskRiskAndReasonTogether()
    {
        var farmId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var signal = new FarmWorkWeatherSignal(
            farmId,
            taskId,
            FarmWorkType.Spraying,
            WeatherActionRiskLevel.High,
            WeatherActionSignalCode.SprayingConditions,
            ["Kuvvetli rüzgâr bekleniyor.", "Uygulama zamanına yakın ciddi yağış riski bulunuyor."],
            WeatherSuggestedAction.DelayConsidered,
            new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc),
            false);
        var context = new AIAccountContext(null,
        [
            new AIFarmSummary(
                farmId, "Kuzey Tarla", "Buğday", 5,
                "İlaçlama", new DateOnly(2026, 9, 4), null, null, null, true,
                signal)
        ]);

        var prompt = AISystemPromptBuilder.Build(context);

        prompt.Should().Contain("İŞ-HAVA DEĞERLENDİRMESİ");
        prompt.Should().Contain("Risk: HIGH");
        prompt.Should().Contain("Öneri: DELAY_CONSIDERED");
        prompt.Should().Contain("Kuvvetli rüzgâr bekleniyor.");
        prompt.Should().Contain("HIGH ise kesin şekilde 'uygundur' deme");
        prompt.Should().Contain("meteorolojik eşik");
    }

    [Fact]
    public void Build_WithLowWorkWeatherSignal_ShouldNotDescribeLowAsGuaranteedSafe()
    {
        var farmId = Guid.NewGuid();
        var signal = new FarmWorkWeatherSignal(
            farmId,
            Guid.NewGuid(),
            FarmWorkType.Irrigation,
            WeatherActionRiskLevel.Low,
            WeatherActionSignalCode.IrrigationTiming,
            ["Tahminlerde bu iş için belirgin bir hava engeli görünmüyor; saha koşullarını yine de kontrol edin."],
            WeatherSuggestedAction.Proceed,
            DateTime.UtcNow,
            false);
        var context = new AIAccountContext(null,
        [
            new AIFarmSummary(farmId, "Güney Tarla", null, null, "Sulama",
                new DateOnly(2026, 9, 4), null, null, null, true, signal)
        ]);

        var prompt = AISystemPromptBuilder.Build(context);

        prompt.Should().Contain("Risk: LOW");
        prompt.Should().Contain("LOW risk, koşulların kesin güvenli olduğu anlamına gelmez");
    }

    [Theory]
    [InlineData(WeatherActionSignalCode.WeatherUnavailable, "WEATHER_UNAVAILABLE")]
    [InlineData(WeatherActionSignalCode.ForecastNotAvailable, "FORECAST_NOT_AVAILABLE")]
    public void Build_WithUnavailableWorkWeatherSignal_ShouldExposeDeterministicStatus(
        WeatherActionSignalCode code,
        string expectedCode)
    {
        var farmId = Guid.NewGuid();
        var signal = new FarmWorkWeatherSignal(
            farmId, Guid.NewGuid(), FarmWorkType.Harvest, null, code,
            ["Planlanan tarih için hava değerlendirmesi yapılamıyor."],
            WeatherSuggestedAction.WeatherUnavailable, DateTime.UtcNow, false);
        var context = new AIAccountContext(null,
        [
            new AIFarmSummary(farmId, "Hasat Tarlası", null, null, "Hasat",
                new DateOnly(2026, 10, 1), null, null, null, true, signal)
        ]);

        AISystemPromptBuilder.Build(context).Should().Contain(expectedCode);
    }
}
