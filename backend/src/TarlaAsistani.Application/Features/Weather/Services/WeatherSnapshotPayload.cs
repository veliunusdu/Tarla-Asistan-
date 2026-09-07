using System.Text.Json;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Weather.Services;

public static class WeatherSnapshotPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string Serialize(double latitude, double longitude, List<WeatherPoint> points) =>
        JsonSerializer.Serialize(new Payload(1, latitude, longitude, points));

    public static List<WeatherPoint>? ReadPoints(string? json, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(json) || !latitude.HasValue || !longitude.HasValue)
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            // Legacy arrays contain no provenance; never assign them the farm's current location.
            if (payload?.Version != 1 || payload.Latitude != latitude || payload.Longitude != longitude)
                return null;

            return payload.Points is { Count: > 0 } ? payload.Points : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Payload(int Version, double? Latitude, double? Longitude, List<WeatherPoint>? Points);
}
