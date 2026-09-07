using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Weather.Services;

public static class WeatherPointSelection
{
    public static WeatherPoint? ClosestTo(IEnumerable<WeatherPoint> points, DateTime referenceTime) =>
        points
            .Where(point => point is not null && point.ObservedAt != default)
            .OrderBy(point => Math.Abs((AsUtc(point.ObservedAt) - AsUtc(referenceTime)).Ticks))
            .ThenBy(point => AsUtc(point.ObservedAt))
            .FirstOrDefault();

    private static DateTime AsUtc(DateTime time) => time.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(time, DateTimeKind.Utc)
        : time.ToUniversalTime();
}
