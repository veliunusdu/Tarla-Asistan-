using System.Globalization;
using System.Text.RegularExpressions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Application.Features.Weather.Services;

public sealed class WeatherActionRiskOptions
{
    public double StrongWindKmh { get; set; } = 30;
    public double BorderlineWindKmh { get; set; } = 20;
    public double HeavyRainProbabilityPct { get; set; } = 70;
    public double HeavyRainMmPerHour { get; set; } = 5;
    public double HeavyRainTotalMm { get; set; } = 10;
    public double MeaningfulRainProbabilityPct { get; set; } = 50;
    public double MeaningfulRainTotalMm { get; set; } = 3;
    public double HighTemperatureC { get; set; } = 30;
    public double ExtremeHeatC { get; set; } = 35;
    public double FrostTemperatureC { get; set; } = 0;
}

public static class FarmWorkTypeNormalizer
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CompareInfo TurkishCompare = TurkishCulture.CompareInfo;
    private static readonly Regex WordRegex = new(@"[\p{L}\p{M}]+", RegexOptions.Compiled);

    private static readonly (FarmWorkType Type, string[][] Variants)[] KnownWorkTypes =
    [
        (FarmWorkType.Spraying, [["ilaçlama"], ["ilaç", "atma"]]),
        (FarmWorkType.Irrigation, [["sulama"]]),
        (FarmWorkType.Fertilizing, [["gübreleme"]]),
        (FarmWorkType.Sowing, [["ekim"]]),
        (FarmWorkType.Harvest, [["hasat"]]),
    ];

    public static FarmWorkType Normalize(string? title, string? description = null)
    {
        var titleType = NormalizeText(title);
        if (titleType != FarmWorkType.Unknown || !string.IsNullOrWhiteSpace(title))
            return titleType;

        return NormalizeText(description);
    }

    private static FarmWorkType NormalizeText(string? value)
    {
        var tokens = Tokenize(value ?? string.Empty);
        foreach (var (type, variants) in KnownWorkTypes)
        {
            if (variants.Any(variant => ContainsSequence(tokens, variant)))
                return type;
        }

        return FarmWorkType.Unknown;
    }

    private static string[] Tokenize(string value) =>
        WordRegex.Matches(value).Select(match => match.Value).ToArray();

    private static bool ContainsSequence(IReadOnlyList<string> source, IReadOnlyList<string> candidate)
    {
        for (var start = 0; start <= source.Count - candidate.Count; start++)
        {
            if (candidate.Select((word, offset) => TurkishCompare.Compare(
                    source[start + offset], word, CompareOptions.IgnoreCase) == 0).All(matches => matches))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class FarmWorkWeatherSignalEvaluator
{
    private readonly WeatherActionRiskOptions _options;

    public FarmWorkWeatherSignalEvaluator(WeatherActionRiskOptions options)
    {
        _options = options;
    }

    public FarmWorkWeatherSignal? Evaluate(FarmWorkWeatherEvaluationInput input)
    {
        var workType = FarmWorkTypeNormalizer.Normalize(input.TaskTitle, input.TaskDescription);
        if (workType == FarmWorkType.Unknown)
            return null;

        if (!input.WeatherAvailable || input.ForecastPoints == null)
        {
            return BuildUnavailable(input, workType, WeatherActionSignalCode.WeatherUnavailable,
                "Güncel hava verisine erişilemediği için iş zamanlaması değerlendirilemiyor.");
        }

        var taskDayPoints = input.ForecastPoints
            .Where(point => DateOnly.FromDateTime(point.ObservedAt) == input.TaskDueDate)
            .ToList();

        if (taskDayPoints.Count == 0)
        {
            return BuildUnavailable(input, workType, WeatherActionSignalCode.ForecastNotAvailable,
                "Planlanan iş tarihi mevcut hava tahmini kapsamının dışında.");
        }

        var summary = ForecastSummary.From(taskDayPoints, _options);
        return workType switch
        {
            FarmWorkType.Spraying => EvaluateSpraying(input, summary),
            FarmWorkType.Irrigation => EvaluateIrrigation(input, summary),
            FarmWorkType.Fertilizing => EvaluateFertilizing(input, summary),
            FarmWorkType.Sowing => EvaluateSowing(input, summary),
            FarmWorkType.Harvest => EvaluateHarvest(input, summary),
            _ => null,
        };
    }

    private FarmWorkWeatherSignal EvaluateSpraying(FarmWorkWeatherEvaluationInput input, ForecastSummary weather)
    {
        var highReasons = new List<string>();
        if (weather.HasStrongWind)
            highReasons.Add("Kuvvetli rüzgâr bekleniyor.");
        if (weather.HasHeavyRain)
            highReasons.Add("Uygulama zamanına yakın ciddi yağış riski bulunuyor.");

        if (highReasons.Count > 0)
            return Build(input, FarmWorkType.Spraying, WeatherActionRiskLevel.High,
                WeatherActionSignalCode.SprayingConditions, highReasons, WeatherSuggestedAction.DelayConsidered);

        var mediumReasons = new List<string>();
        if (weather.HasBorderlineWind)
            mediumReasons.Add("Rüzgâr hızı uygulama zamanlamasının yeniden kontrol edilmesini gerektirebilir.");
        if (weather.HasMeaningfulRain)
            mediumReasons.Add("Yağış ihtimali uygulama zamanlamasını etkileyebilir.");
        if (weather.HasHighTemperature)
            mediumReasons.Add("Yüksek sıcaklık bekleniyor.");

        return mediumReasons.Count > 0
            ? Build(input, FarmWorkType.Spraying, WeatherActionRiskLevel.Medium,
                WeatherActionSignalCode.SprayingConditions, mediumReasons, WeatherSuggestedAction.ReviewTiming)
            : BuildLow(input, FarmWorkType.Spraying, WeatherActionSignalCode.SprayingConditions);
    }

    private FarmWorkWeatherSignal EvaluateIrrigation(FarmWorkWeatherEvaluationInput input, ForecastSummary weather) =>
        weather.HasMeaningfulRain
            ? Build(input, FarmWorkType.Irrigation, WeatherActionRiskLevel.Medium,
                WeatherActionSignalCode.IrrigationTiming,
                ["Önümüzdeki görev gününde anlamlı yağış beklendiği için sulama zamanını yeniden değerlendirin."],
                WeatherSuggestedAction.ReviewTiming)
            : BuildLow(input, FarmWorkType.Irrigation, WeatherActionSignalCode.IrrigationTiming);

    private FarmWorkWeatherSignal EvaluateFertilizing(FarmWorkWeatherEvaluationInput input, ForecastSummary weather)
    {
        if (weather.HasHeavyRain)
        {
            return Build(input, FarmWorkType.Fertilizing, WeatherActionRiskLevel.High,
                WeatherActionSignalCode.FertilizingConditions,
                ["Yoğun yağış, yüzey uygulamalarında yıkanma riskini artırabilir."],
                WeatherSuggestedAction.DelayConsidered);
        }

        var reasons = new List<string>();
        if (weather.HasStrongWind || weather.HasBorderlineWind)
            reasons.Add("Rüzgâr, yüzey uygulamalarının zamanlamasını etkileyebilir.");
        if (weather.HasMeaningfulRain)
            reasons.Add("Yağış ihtimali gübreleme zamanlamasının yeniden kontrol edilmesini gerektirebilir.");

        return reasons.Count > 0
            ? Build(input, FarmWorkType.Fertilizing, WeatherActionRiskLevel.Medium,
                WeatherActionSignalCode.FertilizingConditions, reasons, WeatherSuggestedAction.ReviewTiming)
            : BuildLow(input, FarmWorkType.Fertilizing, WeatherActionSignalCode.FertilizingConditions);
    }

    private FarmWorkWeatherSignal EvaluateSowing(FarmWorkWeatherEvaluationInput input, ForecastSummary weather)
    {
        var highReasons = new List<string>();
        if (weather.HasHeavyRain)
            highReasons.Add("Yoğun yağış ekim zamanlamasını olumsuz etkileyebilir.");
        if (weather.HasFrost)
            highReasons.Add("Görev gününde don riski bulunuyor.");
        if (weather.HasExtremeHeat)
            highReasons.Add("Görev gününde aşırı sıcaklık bekleniyor.");

        if (highReasons.Count > 0)
            return Build(input, FarmWorkType.Sowing, WeatherActionRiskLevel.High,
                WeatherActionSignalCode.SowingConditions, highReasons, WeatherSuggestedAction.DelayConsidered);

        return weather.HasMeaningfulRain || weather.HasHighTemperature
            ? Build(input, FarmWorkType.Sowing, WeatherActionRiskLevel.Medium,
                WeatherActionSignalCode.SowingConditions,
                ["Yağış ve sıcaklık koşullarını ekim öncesinde yeniden kontrol edin."],
                WeatherSuggestedAction.ReviewTiming)
            : BuildLow(input, FarmWorkType.Sowing, WeatherActionSignalCode.SowingConditions);
    }

    private FarmWorkWeatherSignal EvaluateHarvest(FarmWorkWeatherEvaluationInput input, ForecastSummary weather)
    {
        var highReasons = new List<string>();
        if (weather.HasHeavyRain)
            highReasons.Add("Yoğun yağış hasat zamanlamasını olumsuz etkileyebilir.");
        if (weather.HasStrongWind)
            highReasons.Add("Kuvvetli rüzgâr hasat koşullarını olumsuz etkileyebilir.");

        if (highReasons.Count > 0)
            return Build(input, FarmWorkType.Harvest, WeatherActionRiskLevel.High,
                WeatherActionSignalCode.HarvestConditions, highReasons, WeatherSuggestedAction.DelayConsidered);

        return weather.HasMeaningfulRain || weather.HasBorderlineWind
            ? Build(input, FarmWorkType.Harvest, WeatherActionRiskLevel.Medium,
                WeatherActionSignalCode.HarvestConditions,
                ["Yağış veya rüzgâr ihtimali nedeniyle hasat zamanlamasını yeniden kontrol edin."],
                WeatherSuggestedAction.ReviewTiming)
            : BuildLow(input, FarmWorkType.Harvest, WeatherActionSignalCode.HarvestConditions);
    }

    private static FarmWorkWeatherSignal BuildLow(
        FarmWorkWeatherEvaluationInput input,
        FarmWorkType workType,
        WeatherActionSignalCode code) =>
        Build(input, workType, WeatherActionRiskLevel.Low, code,
            ["Tahminlerde bu iş için belirgin bir hava engeli görünmüyor; saha koşullarını yine de kontrol edin."],
            WeatherSuggestedAction.Proceed);

    private static FarmWorkWeatherSignal BuildUnavailable(
        FarmWorkWeatherEvaluationInput input,
        FarmWorkType workType,
        WeatherActionSignalCode code,
        string reason) =>
        Build(input, workType, null, code, [reason], WeatherSuggestedAction.WeatherUnavailable);

    private static FarmWorkWeatherSignal Build(
        FarmWorkWeatherEvaluationInput input,
        FarmWorkType workType,
        WeatherActionRiskLevel? level,
        WeatherActionSignalCode code,
        List<string> reasons,
        WeatherSuggestedAction action) =>
        new(input.FarmId, input.TaskId, workType, level, code, reasons, action,
            input.EvaluatedAtUtc, input.IsStaleWeather);

    private sealed record ForecastSummary(
        bool HasStrongWind,
        bool HasBorderlineWind,
        bool HasHeavyRain,
        bool HasMeaningfulRain,
        bool HasHighTemperature,
        bool HasExtremeHeat,
        bool HasFrost)
    {
        public static ForecastSummary From(IReadOnlyList<WeatherPoint> points, WeatherActionRiskOptions options)
        {
            var maxWind = points.Where(point => point.WindSpeedKmh.HasValue)
                .Select(point => point.WindSpeedKmh!.Value).DefaultIfEmpty().Max();
            var maxRainProbability = points.Where(point => point.PrecipitationProbability.HasValue)
                .Select(point => point.PrecipitationProbability!.Value).DefaultIfEmpty().Max();
            var maxHourlyRain = points.Where(point => point.PrecipitationMm.HasValue)
                .Select(point => point.PrecipitationMm!.Value).DefaultIfEmpty().Max();
            var totalRain = points.Sum(point => point.PrecipitationMm ?? 0);
            var maxTemperature = points.Where(point => point.TemperatureC.HasValue)
                .Select(point => point.TemperatureC!.Value).DefaultIfEmpty(double.MinValue).Max();
            var minTemperature = points.Where(point => point.TemperatureC.HasValue)
                .Select(point => point.TemperatureC!.Value).DefaultIfEmpty(double.MaxValue).Min();

            return new ForecastSummary(
                HasStrongWind: maxWind >= options.StrongWindKmh,
                HasBorderlineWind: maxWind >= options.BorderlineWindKmh,
                HasHeavyRain: maxRainProbability >= options.HeavyRainProbabilityPct &&
                              (maxHourlyRain >= options.HeavyRainMmPerHour || totalRain >= options.HeavyRainTotalMm),
                HasMeaningfulRain: totalRain >= options.MeaningfulRainTotalMm ||
                                   (maxRainProbability >= options.MeaningfulRainProbabilityPct && totalRain > 0),
                HasHighTemperature: maxTemperature >= options.HighTemperatureC,
                HasExtremeHeat: maxTemperature >= options.ExtremeHeatC,
                HasFrost: minTemperature <= options.FrostTemperatureC);
        }
    }
}
