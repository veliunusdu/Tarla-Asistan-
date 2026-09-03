using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class ProactiveAdvisoryEngineTests
{
    private readonly ProactiveAdvisoryEngine _engine = new();
    private readonly Farm _farm = new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Name = "Kuzey Tarlası",
        Latitude = 39.92077,
        Longitude = 32.85411,
        IrrigationMethod = IrrigationMethod.Drip,
        SoilType = "Tınlı"
    };

    [Fact]
    public void Evaluate_WhenHeavyRainAndPlannedFertilization_ShouldProduceFertilizerDelay()
    {
        var nowUtc = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(nowUtc);
        var tomorrow = today.AddDays(1);

        var task = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farm.Id,
            Title = "Azot Üst Gübrelemesi",
            Description = "Mısır için üre gübresi atılacak.",
            DueDate = tomorrow,
            Status = TaskStatus.Planned,
            DedupeKey = "task-fert-1"
        };

        var points = new List<WeatherPoint>
        {
            // Tomorrow heavy rain: 16 mm
            new(nowUtc.AddDays(1), TemperatureC: 18, PrecipitationProbability: 85, PrecipitationMm: 16, WindSpeedKmh: 12),
            // Day after tomorrow: clear
            new(nowUtc.AddDays(2), TemperatureC: 20, PrecipitationProbability: 10, PrecipitationMm: 0, WindSpeedKmh: 10)
        };

        var forecast = new WeatherForecastData(points);

        var advisories = _engine.Evaluate(_farm, [], [task], forecast, nowUtc);

        advisories.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.FertilizerDelay);
        var adv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.FertilizerDelay);
        adv.Severity.Should().Be(AdvisorySeverity.Critical);
        adv.ActionType.Should().Be(ProactiveActionType.PostponeTask);
        adv.RelatedTaskId.Should().Be(task.Id);
        adv.RecommendedDate.Should().Be(today.AddDays(2));
        adv.Summary.Should().Contain("yağış");
        adv.ActionRecommendation.Should().Contain("erteleyin");
    }

    [Fact]
    public void Evaluate_WhenRainExpectedAndNotRainfed_ShouldProduceIrrigationSuppression()
    {
        var nowUtc = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        var task = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farm.Id,
            Title = "Damla Sulama",
            Description = "4 saatlik sulama",
            DueDate = DateOnly.FromDateTime(nowUtc).AddDays(1),
            Status = TaskStatus.Planned,
            DedupeKey = "task-irr-1"
        };

        var points = new List<WeatherPoint>
        {
            new(nowUtc.AddDays(1), TemperatureC: 22, PrecipitationProbability: 80, PrecipitationMm: 14, WindSpeedKmh: 10)
        };

        var forecast = new WeatherForecastData(points);

        var advisories = _engine.Evaluate(_farm, [], [task], forecast, nowUtc);

        advisories.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.IrrigationSuppression);
        var adv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.IrrigationSuppression);
        adv.ActionType.Should().Be(ProactiveActionType.CancelOrPostponeIrrigation);
        adv.Summary.Should().Contain("tasarruf");
    }

    [Fact]
    public void Evaluate_WhenHighWindOnSprayingDay_ShouldProduceSprayingWindowWithAlternativeDate()
    {
        var nowUtc = new DateTime(2026, 5, 15, 8, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(nowUtc);
        var taskDate = today.AddDays(1);
        var calmDate = today.AddDays(2);

        var task = new FarmTask
        {
            Id = Guid.NewGuid(),
            FarmId = _farm.Id,
            Title = "Yaprak Biti İlaçlaması",
            Description = "İnsektisit uygulaması",
            DueDate = taskDate,
            Status = TaskStatus.Planned,
            DedupeKey = "task-spray-1"
        };

        var points = new List<WeatherPoint>
        {
            // High wind on task day: 26 km/h
            new(nowUtc.AddDays(1), TemperatureC: 24, PrecipitationProbability: 5, PrecipitationMm: 0, WindSpeedKmh: 26),
            // Calm on next day: 8 km/h
            new(nowUtc.AddDays(2), TemperatureC: 22, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        var forecast = new WeatherForecastData(points);

        var advisories = _engine.Evaluate(_farm, [], [task], forecast, nowUtc);

        advisories.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.SprayingWindow);
        var adv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.SprayingWindow);
        adv.Severity.Should().Be(AdvisorySeverity.Warning);
        adv.ActionType.Should().Be(ProactiveActionType.RescheduleSpraying);
        adv.RecommendedDate.Should().Be(calmDate);
        adv.Summary.Should().Contain("sürüklenme");
    }

    [Fact]
    public void Evaluate_WhenFreezingTemperatureExpected_ShouldProduceCriticalFrostAlert()
    {
        var nowUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);

        var points = new List<WeatherPoint>
        {
            new(nowUtc.AddHours(20), TemperatureC: 5, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 5),
            new(nowUtc.AddHours(28), TemperatureC: -1.8, PrecipitationProbability: 0, PrecipitationMm: 0, WindSpeedKmh: 4)
        };

        var forecast = new WeatherForecastData(points);

        var advisories = _engine.Evaluate(_farm, [], [], forecast, nowUtc);

        advisories.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.FrostAlert);
        var adv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.FrostAlert);
        adv.Severity.Should().Be(AdvisorySeverity.Critical);
        adv.ActionType.Should().Be(ProactiveActionType.EmergencyProtection);
        adv.Summary.Should().ContainEquivalentOf("don");
    }

    [Fact]
    public void Evaluate_WhenHighHumidityAndWarmTemperature_ShouldProduceFungalDiseaseRisk()
    {
        var nowUtc = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);

        // 24 hours of humidity >= 80% and temp 21°C
        var points = new List<WeatherPoint>();
        for (int h = 0; h < 26; h++)
        {
            points.Add(new(nowUtc.AddHours(h), TemperatureC: 21, PrecipitationProbability: 40, PrecipitationMm: 0.2, WindSpeedKmh: 6, HumidityPercent: 85));
        }

        var forecast = new WeatherForecastData(points);

        var advisories = _engine.Evaluate(_farm, [], [], forecast, nowUtc);

        advisories.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.FungalDiseaseRisk);
        var adv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.FungalDiseaseRisk);
        adv.ActionType.Should().Be(ProactiveActionType.FieldScouting);
        adv.Summary.Should().Contain("mantari");
    }
}
