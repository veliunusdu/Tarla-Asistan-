using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class FarmWorkWeatherSignalEvaluatorTests
{
    private static readonly DateOnly TaskDate = new(2026, 9, 4);
    private static readonly DateTime EvaluatedAtUtc = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FarmId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    private readonly FarmWorkWeatherSignalEvaluator _sut = new(new WeatherActionRiskOptions());

    [Theory]
    [InlineData("İlaçlama", FarmWorkType.Spraying)]
    [InlineData("İlaç atma", FarmWorkType.Spraying)]
    [InlineData("Damla sulama", FarmWorkType.Irrigation)]
    [InlineData("Taban gübreleme", FarmWorkType.Fertilizing)]
    [InlineData("Buğday ekim", FarmWorkType.Sowing)]
    [InlineData("Arpa hasat", FarmWorkType.Harvest)]
    [InlineData("İlaç deposunu kontrol et", FarmWorkType.Unknown)]
    [InlineData("Tarla kontrolü", FarmWorkType.Unknown)]
    public void Normalize_UsesSmallExactTurkishVocabulary(string title, FarmWorkType expected)
    {
        FarmWorkTypeNormalizer.Normalize(title).Should().Be(expected);
    }

    [Fact]
    public void Normalize_GenericTaskDoesNotUseExampleWorkNamesInDescription()
    {
        FarmWorkTypeNormalizer.Normalize(
                "Tarla günlüğünü güncelleyin",
                "Son sulama, gübreleme, ilaçlama veya saha kontrolü gibi işlemleri kaydedin.")
            .Should().Be(FarmWorkType.Unknown);
    }

    [Fact]
    public void Evaluate_SprayingWithStrongWind_ReturnsHigh()
    {
        var signal = Evaluate("İlaçlama", Point(wind: 32));

        signal.Should().NotBeNull();
        signal!.RiskLevel.Should().Be(WeatherActionRiskLevel.High);
        signal.Code.Should().Be(WeatherActionSignalCode.SprayingConditions);
        signal.SuggestedAction.Should().Be(WeatherSuggestedAction.DelayConsidered);
        signal.Reasons.Should().Contain(reason => reason.Contains("rüzgâr"));
    }

    [Fact]
    public void Evaluate_SprayingWithHeavyRain_ReturnsHigh()
    {
        var signal = Evaluate("İlaç atma", Point(rainProbability: 85, rainMm: 6));

        signal!.RiskLevel.Should().Be(WeatherActionRiskLevel.High);
        signal.Reasons.Should().Contain(reason => reason.Contains("yağış"));
    }

    [Fact]
    public void Evaluate_SprayingWithMildWeather_ReturnsLowWithoutSafetyGuarantee()
    {
        var signal = Evaluate("İlaçlama", Point(temperature: 22, wind: 8, rainProbability: 10));

        signal!.RiskLevel.Should().Be(WeatherActionRiskLevel.Low);
        signal.SuggestedAction.Should().Be(WeatherSuggestedAction.Proceed);
        signal.Reasons.Should().ContainSingle()
            .Which.Should().Contain("belirgin bir hava engeli");
    }

    [Fact]
    public void Evaluate_SprayingWithBorderlineWindAndHighTemperature_ReturnsMedium()
    {
        var signal = Evaluate("İlaçlama", Point(temperature: 31, wind: 22));

        signal!.RiskLevel.Should().Be(WeatherActionRiskLevel.Medium);
        signal.SuggestedAction.Should().Be(WeatherSuggestedAction.ReviewTiming);
    }

    [Fact]
    public void Evaluate_IrrigationWithMeaningfulRain_ReturnsMediumReviewTiming()
    {
        var signal = Evaluate(
            "Sulama",
            Point(hour: 8, rainProbability: 65, rainMm: 2),
            Point(hour: 14, rainProbability: 70, rainMm: 2));

        signal!.RiskLevel.Should().Be(WeatherActionRiskLevel.Medium);
        signal.Code.Should().Be(WeatherActionSignalCode.IrrigationTiming);
        signal.SuggestedAction.Should().Be(WeatherSuggestedAction.ReviewTiming);
    }

    [Fact]
    public void Evaluate_IrrigationWithDryForecast_ReturnsLow()
    {
        Evaluate("Sulama", Point(rainProbability: 5, rainMm: 0))!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.Low);
    }

    [Fact]
    public void Evaluate_FertilizingWithHeavyRain_ReturnsHigh()
    {
        Evaluate("Gübreleme", Point(rainProbability: 80, rainMm: 8))!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.High);
    }

    [Fact]
    public void Evaluate_SowingWithFrost_ReturnsHigh()
    {
        Evaluate("Ekim", Point(temperature: -1))!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.High);
    }

    [Fact]
    public void Evaluate_HarvestWithHeavyRain_ReturnsHigh()
    {
        Evaluate("Hasat", Point(rainProbability: 90, rainMm: 7))!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.High);
    }

    [Fact]
    public void Evaluate_UnknownWork_ReturnsNoSignal()
    {
        Evaluate("Genel saha kontrolü", Point(wind: 40)).Should().BeNull();
    }

    [Fact]
    public void Evaluate_StaleWeather_PreservesStaleFlag()
    {
        Evaluate("Sulama", true, Point(rainProbability: 70, rainMm: 5))!
            .IsBasedOnStaleWeather.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MissingWeather_ReturnsWeatherUnavailable()
    {
        var signal = _sut.Evaluate(Input("Sulama", points: null, weatherAvailable: false));

        signal!.RiskLevel.Should().BeNull();
        signal.Code.Should().Be(WeatherActionSignalCode.WeatherUnavailable);
        signal.SuggestedAction.Should().Be(WeatherSuggestedAction.WeatherUnavailable);
    }

    [Fact]
    public void Evaluate_NoForecastOnTaskDate_ReturnsForecastNotAvailable()
    {
        var outsideHorizon = new WeatherPoint(
            TaskDate.AddDays(-1).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            22, 10, 0, 5);

        var signal = Evaluate("Sulama", outsideHorizon);

        signal!.RiskLevel.Should().BeNull();
        signal.Code.Should().Be(WeatherActionSignalCode.ForecastNotAvailable);
    }

    [Fact]
    public void Evaluate_UsesTaskDateForecastInsteadOfCurrentDay()
    {
        var currentDayStorm = new WeatherPoint(
            TaskDate.AddDays(-1).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            20, 95, 10, 45);
        var taskDayMild = Point(temperature: 22, rainProbability: 5, rainMm: 0, wind: 6);

        Evaluate("İlaçlama", currentDayStorm, taskDayMild)!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.Low);
    }

    [Fact]
    public void Evaluate_UsesConfiguredThresholds()
    {
        var evaluator = new FarmWorkWeatherSignalEvaluator(new WeatherActionRiskOptions
        {
            StrongWindKmh = 40,
            BorderlineWindKmh = 35,
        });

        evaluator.Evaluate(Input("İlaçlama", [Point(wind: 32)]))!
            .RiskLevel.Should().Be(WeatherActionRiskLevel.Low);
    }

    private FarmWorkWeatherSignal? Evaluate(string title, params WeatherPoint[] points) =>
        _sut.Evaluate(Input(title, points));

    private FarmWorkWeatherSignal? Evaluate(string title, bool stale, params WeatherPoint[] points) =>
        _sut.Evaluate(Input(title, points, isStale: stale));

    private static FarmWorkWeatherEvaluationInput Input(
        string title,
        IReadOnlyList<WeatherPoint>? points,
        bool weatherAvailable = true,
        bool isStale = false) =>
        new(FarmId, TaskId, title, null, TaskDate, points, weatherAvailable, isStale, EvaluatedAtUtc);

    private static WeatherPoint Point(
        int hour = 12,
        double? temperature = 22,
        double? rainProbability = 0,
        double? rainMm = 0,
        double? wind = 5) =>
        new(
            TaskDate.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc),
            temperature,
            rainProbability,
            rainMm,
            wind);
}
