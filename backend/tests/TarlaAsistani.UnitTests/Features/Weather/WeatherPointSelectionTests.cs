using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Services;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class WeatherPointSelectionTests
{
    [Fact]
    public void ClosestTo_UnsortedPoints_SelectsNearestInstant()
    {
        var now = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        List<WeatherPoint> points = [new(now.AddHours(-12), 3, 0, 0, 1), new(now.AddHours(8), 15, 0, 0, 2), new(now, 25, 0, 0, 12)];

        WeatherPointSelection.ClosestTo(points, now).Should().BeSameAs(points[2]);
    }

    [Fact]
    public void ClosestTo_EquidistantPoints_PrefersEarlierPoint()
    {
        var now = DateTime.UtcNow;
        List<WeatherPoint> points = [new(now.AddHours(1), 30, 0, 0, 1), new(now.AddHours(-1), 20, 0, 0, 2)];

        WeatherPointSelection.ClosestTo(points, now).Should().BeSameAs(points[1]);
    }

    [Fact]
    public void ClosestTo_LocalTimestamp_IsComparedAsAnInstant()
    {
        var now = DateTime.UtcNow;
        List<WeatherPoint> points = [new(now.AddHours(-6), 3, 0, 0, 1), new(now.ToLocalTime(), 25, 0, 0, 2)];

        WeatherPointSelection.ClosestTo(points, now).Should().BeSameAs(points[1]);
    }

    [Fact]
    public void ClosestTo_MissingTimestampOrEmptyList_ReturnsNull()
    {
        WeatherPointSelection.ClosestTo([], DateTime.UtcNow).Should().BeNull();
        WeatherPointSelection.ClosestTo([new(default, 3, 0, 0, 1)], DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public async Task DefaultProviderMapping_SelectsCurrentHourInsteadOfMidnight()
    {
        var now = DateTime.UtcNow;
        List<WeatherPoint> points = [new(now.AddHours(-12), 3, 0, 0, 1), new(now, 25, 0, 0, 12)];
        IWeatherProvider provider = new PointsOnlyProvider(points);

        var result = await provider.GetWeatherAsync(39, 32);

        result.Current!.TemperatureC.Should().Be(25);
        result.Current.ObservedAt.Should().Be(now);
    }

    [Fact]
    public void AiContext_PointsOnlyFallback_SelectsNearestAndPreservesStaleFlag()
    {
        var now = DateTime.UtcNow;
        List<WeatherPoint> points = [new(now.AddHours(-12), 3, 0, 0, 1), new(now, 25, 0, 0, 12, 40)];
        var dto = new FarmWeatherResponseDto(Guid.NewGuid(), "test", now.AddHours(-2), true, "Cached forecast", points, []);

        var context = dto.ToAiContext();

        context.CurrentTemperatureC.Should().Be(25);
        context.CurrentHumidityPercent.Should().Be(40);
        context.ObservedAtUtc.Should().Be(now);
        context.IsStale.Should().BeTrue();
        context.StaleReason.Should().Be("Cached forecast");
    }

    private sealed class PointsOnlyProvider(List<WeatherPoint> points) : IWeatherProvider
    {
        public string Name => "test";
        public Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default) => Task.FromResult(points);
    }
}
