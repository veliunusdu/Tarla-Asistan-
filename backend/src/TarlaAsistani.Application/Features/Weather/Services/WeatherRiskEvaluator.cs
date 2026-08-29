using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Application.Features.Weather.Services;

public static class WeatherRiskEvaluator
{
    private const double FrostTemperatureC = 0.0;
    private const double StrongWindKmh = 30.0;
    private const double HeavyRainProbability = 70.0;
    private const double HeavyRainMmPerHour = 5.0;

    public static List<WeatherRiskDto> Evaluate(List<WeatherPoint> points, DateTime? referenceTime = null)
    {
        var reference = referenceTime ?? DateTime.UtcNow;
        var futurePoints = points.Where(p => p.ObservedAt >= reference).ToList();
        var risks = new List<WeatherRiskDto>();

        // 1. Frost risk (next 24 hours)
        var frostDeadline = reference.AddHours(24);
        var frostPoints = futurePoints
            .Where(p => p.ObservedAt <= frostDeadline && p.TemperatureC.HasValue && p.TemperatureC.Value <= FrostTemperatureC)
            .ToList();

        if (frostPoints.Count > 0)
        {
            risks.Add(new WeatherRiskDto(
                RiskType: "FROST",
                Severity: "CRITICAL",
                StartsAt: frostPoints.Min(p => p.ObservedAt),
                EndsAt: frostPoints.Max(p => p.ObservedAt),
                Message: "Önümüzdeki 24 saatte don riski görülebilir.",
                SuggestedAction: "Hassas ürünleri kontrol edin ve bölgenize uygun koruma önlemlerini bir uzmana danışarak değerlendirin."
            ));
        }

        // 2. Strong wind risk (next 24 hours)
        var windDeadline = reference.AddHours(24);
        var windPoints = futurePoints
            .Where(p => p.ObservedAt <= windDeadline && p.WindSpeedKmh.HasValue && p.WindSpeedKmh.Value >= StrongWindKmh)
            .ToList();

        if (windPoints.Count > 0)
        {
            risks.Add(new WeatherRiskDto(
                RiskType: "STRONG_WIND",
                Severity: "HIGH",
                StartsAt: windPoints.Min(p => p.ObservedAt),
                EndsAt: windPoints.Max(p => p.ObservedAt),
                Message: "Önümüzdeki 24 saatte kuvvetli rüzgâr görülebilir.",
                SuggestedAction: "İlaçlama planını ertelemeyi değerlendirin; saha koşullarını yerinde kontrol etmeden uygulama yapmayın."
            ));
        }

        // 3. Heavy rain risk (next 12 hours)
        var rainDeadline = reference.AddHours(12);
        var rainPoints = futurePoints
            .Where(p => p.ObservedAt <= rainDeadline &&
                        p.PrecipitationProbability.HasValue && p.PrecipitationProbability.Value >= HeavyRainProbability &&
                        p.PrecipitationMm.HasValue && p.PrecipitationMm.Value >= HeavyRainMmPerHour)
            .ToList();

        if (rainPoints.Count > 0)
        {
            risks.Add(new WeatherRiskDto(
                RiskType: "HEAVY_RAIN",
                Severity: "HIGH",
                StartsAt: rainPoints.Min(p => p.ObservedAt),
                EndsAt: rainPoints.Max(p => p.ObservedAt),
                Message: "Önümüzdeki 12 saatte yoğun yağış riski görülebilir.",
                SuggestedAction: "Sulama planını yeniden değerlendirin ve drenajı kontrol edin; kararı yerel koşullara göre verin."
            ));
        }

        return risks;
    }
}
