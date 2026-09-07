using System.Text.Json;
using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.Services;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class WeatherSnapshotPayloadTests
{
    private static List<WeatherPoint> Points() => [new(DateTime.UtcNow, 20, 30, 1, 5)];

    [Theory]
    [InlineData(39.2, 32.2)]
    [InlineData(0, 0)]
    [InlineData(-38.1, -32.3)]
    public void ReadPoints_WithMatchingCoordinates_RoundTrips(double latitude, double longitude)
    {
        var points = Points();
        var json = WeatherSnapshotPayload.Serialize(latitude, longitude, points);

        WeatherSnapshotPayload.ReadPoints(json, latitude, longitude).Should().BeEquivalentTo(points);
    }

    [Theory]
    [InlineData(37.1, 32.2)]
    [InlineData(39.2, 30.1)]
    [InlineData(null, 32.2)]
    [InlineData(39.2, null)]
    public void ReadPoints_WithDifferentOrMissingCoordinates_ReturnsNull(double? latitude, double? longitude)
    {
        var json = WeatherSnapshotPayload.Serialize(39.2, 32.2, Points());

        WeatherSnapshotPayload.ReadPoints(json, latitude, longitude).Should().BeNull();
    }

    [Fact]
    public void ReadPoints_LegacyArray_HasNoVerifiableLocation()
    {
        WeatherSnapshotPayload.ReadPoints(JsonSerializer.Serialize(Points()), 39.2, 32.2).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("broken json")]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"Latitude\":0,\"Longitude\":0,\"Points\":[]}")]
    [InlineData("{\"Version\":1,\"Points\":[]}")]
    public void ReadPoints_InvalidPayload_IsNotUsable(string? json)
    {
        WeatherSnapshotPayload.ReadPoints(json, 0, 0).Should().BeNull();
    }
}
