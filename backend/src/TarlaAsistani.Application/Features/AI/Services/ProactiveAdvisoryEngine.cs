using System.Text.Json;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.AI.Services;

public class ProactiveAdvisoryEngine : IProactiveAdvisoryEngine
{
    public IReadOnlyList<ProactiveAdvisoryEvaluationResult> Evaluate(
        Farm farm,
        IReadOnlyList<Activity> pastActivities,
        IReadOnlyList<FarmTask> upcomingTasks,
        WeatherForecastData? forecast,
        DateTime nowUtc)
    {
        var results = new List<ProactiveAdvisoryEvaluationResult>();
        var points = forecast?.Points ?? [];
        if (points.Count == 0) return results;

        var today = DateOnly.FromDateTime(nowUtc);

        // ── 1. Gübre Yıkanma & Erteleme Kuralı (Fertilizer Delay) ───────────────
        EvaluateFertilizerDelay(farm, upcomingTasks, points, today, results);

        // ── 2. Sulama Tasarrufu Kuralı (Irrigation Suppression) ───────────────────
        EvaluateIrrigationSuppression(farm, upcomingTasks, points, today, nowUtc, results);

        // ── 3. İlaçlama Rüzgar & Pencere Kuralı (Spraying Window) ────────────────
        EvaluateSprayingWindow(farm, upcomingTasks, points, today, results);

        // ── 4. Don Erken Uyarısı (Frost Alert) ──────────────────────────────────
        EvaluateFrostAlert(farm, points, results);

        // ── 5. Mantari Hastalık Enfeksiyon İndeksi (Fungal Disease Risk) ─────────
        EvaluateFungalDiseaseRisk(farm, points, nowUtc, results);

        return results;
    }

    private static void EvaluateFertilizerDelay(
        Farm farm,
        IReadOnlyList<FarmTask> upcomingTasks,
        List<WeatherPoint> points,
        DateOnly today,
        List<ProactiveAdvisoryEvaluationResult> results)
    {
        var activeTasks = upcomingTasks
            .Where(t => t.Status is TaskStatus.New or TaskStatus.Planned or TaskStatus.Viewed)
            .Where(t => IsFertilizerTask(t.Title, t.Description));

        // Soil threshold: sandy soils leach with even 6 mm; others 8 mm
        var rainThresholdMm = (farm.SoilType?.Contains("kum", StringComparison.OrdinalIgnoreCase) == true) ? 6.0 : 8.0;

        foreach (var task in activeTasks)
        {
            var taskDate = task.DueDate;
            if (taskDate < today || taskDate > today.AddDays(4)) continue;

            // Check rainfall around task date (task day + next day)
            var rainPoints = points
                .Where(p => DateOnly.FromDateTime(p.ObservedAt) >= taskDate &&
                            DateOnly.FromDateTime(p.ObservedAt) <= taskDate.AddDays(1))
                .ToList();

            var totalRainMm = rainPoints.Sum(p => p.PrecipitationMm ?? 0);
            var maxProb = rainPoints.Count > 0 ? rainPoints.Max(p => p.PrecipitationProbability ?? 0) : 0;

            if (totalRainMm >= rainThresholdMm || maxProb >= 75)
            {
                // Find next dry day after rain (rain < 2 mm)
                var nextDryDate = taskDate.AddDays(1);
                while (nextDryDate <= today.AddDays(7))
                {
                    var dayPoints = points.Where(p => DateOnly.FromDateTime(p.ObservedAt) == nextDryDate).ToList();
                    var dayRain = dayPoints.Sum(p => p.PrecipitationMm ?? 0);
                    if (dayRain < 2.0) break;
                    nextDryDate = nextDryDate.AddDays(1);
                }

                var dedupeKey = $"adv-fert-{farm.Id}-{task.Id}-{taskDate:yyyyMMdd}";
                var metrics = JsonSerializer.Serialize(new
                {
                    expected_rain_mm = Math.Round(totalRainMm, 1),
                    precipitation_prob = maxProb,
                    soil_type = farm.SoilType ?? "Standart"
                });

                results.Add(new ProactiveAdvisoryEvaluationResult(
                    AdvisoryType: ProactiveAdvisoryType.FertilizerDelay,
                    Severity: totalRainMm >= 15 ? AdvisorySeverity.Critical : AdvisorySeverity.Warning,
                    ActionType: ProactiveActionType.PostponeTask,
                    Title: "Şiddetli Yağış Uyarısı: Gübrelemeyi Erteleyin",
                    Summary: $"Önümüzdeki günlerde tarlanıza yaklaşık {totalRainMm:F1} mm yağış bekleniyor. Gübrenin yıkanıp kaybolmaması için erteleme önerilir.",
                    AgronomicExplanation: $"Toprak yüzeyine atılan azotlu ve granül gübreler, {totalRainMm:F1} mm ve üzeri yağışlarda kök derinliğinin altına yıkanarak (leaching) ya da yüzey akışıyla tarladan uzaklaşır. Bu hem bitki besin alımını engeller hem de ciddi ekonomik kayba yol açar.",
                    ActionRecommendation: $"Planlanan gübreleme uygulamasını yağış sonrasına ({nextDryDate:d MMMM dddd}) erteleyin.",
                    RecommendedDate: nextDryDate,
                    DedupeKey: dedupeKey,
                    RelatedTaskId: task.Id,
                    MetricsJson: metrics));
            }
        }
    }

    private static void EvaluateIrrigationSuppression(
        Farm farm,
        IReadOnlyList<FarmTask> upcomingTasks,
        List<WeatherPoint> points,
        DateOnly today,
        DateTime nowUtc,
        List<ProactiveAdvisoryEvaluationResult> results)
    {
        if (farm.IrrigationMethod == IrrigationMethod.Rainfed) return;

        // Check rainfall in the next 48 hours
        var next48HoursPoints = points
            .Where(p => p.ObservedAt >= nowUtc && p.ObservedAt <= nowUtc.AddHours(48))
            .ToList();

        var totalRainMm = next48HoursPoints.Sum(p => p.PrecipitationMm ?? 0);
        var maxProb = next48HoursPoints.Count > 0 ? next48HoursPoints.Max(p => p.PrecipitationProbability ?? 0) : 0;

        if (totalRainMm >= 6.0 || maxProb >= 70)
        {
            var plannedIrrigationTask = upcomingTasks
                .FirstOrDefault(t => t.Status is TaskStatus.New or TaskStatus.Planned &&
                                     IsIrrigationTask(t.Title, t.Description) &&
                                     t.DueDate >= today && t.DueDate <= today.AddDays(2));

            var dedupeKey = $"adv-irrig-{farm.Id}-{today:yyyyMMdd}";
            var metrics = JsonSerializer.Serialize(new
            {
                expected_rain_mm = Math.Round(totalRainMm, 1),
                savings_note = "Doğal yağış sulama ihtiyacını karşılayacaktır."
            });

            results.Add(new ProactiveAdvisoryEvaluationResult(
                AdvisoryType: ProactiveAdvisoryType.IrrigationSuppression,
                Severity: AdvisorySeverity.Warning,
                ActionType: ProactiveActionType.CancelOrPostponeIrrigation,
                Title: "Doğal Yağış Geliyor: Sulamayı Durdurarak Tasarruf Edin",
                Summary: $"Önümüzdeki 48 saatte beklenen {totalRainMm:F1} mm doğal yağış toprak nemini karşılayacaktır. Sulamayı durdurarak su ve enerji tasarrufu yapabilirsiniz.",
                AgronomicExplanation: $"Tarlaya düşecek {totalRainMm:F1} mm yağış kök derinliğindeki tarla kapasitesini doyuracaktır. Yağış öncesi yapılan sulama toprakta aşırı doygunluğa (asfiksi) ve kök boğulmasına neden olabilir.",
                ActionRecommendation: "Planlanan sulama vanalarını kapalı tutun ve yağış sonrası toprak nemini kontrol edin.",
                RecommendedDate: null,
                DedupeKey: dedupeKey,
                RelatedTaskId: plannedIrrigationTask?.Id,
                MetricsJson: metrics));
        }
    }

    private static void EvaluateSprayingWindow(
        Farm farm,
        IReadOnlyList<FarmTask> upcomingTasks,
        List<WeatherPoint> points,
        DateOnly today,
        List<ProactiveAdvisoryEvaluationResult> results)
    {
        var activeSprayingTasks = upcomingTasks
            .Where(t => t.Status is TaskStatus.New or TaskStatus.Planned or TaskStatus.Viewed)
            .Where(t => IsSprayingTask(t.Title, t.Description));

        foreach (var task in activeSprayingTasks)
        {
            var taskDate = task.DueDate;
            if (taskDate < today || taskDate > today.AddDays(3)) continue;

            var taskPoints = points
                .Where(p => DateOnly.FromDateTime(p.ObservedAt) == taskDate)
                .ToList();

            var maxWind = taskPoints.Count > 0 ? taskPoints.Max(p => p.WindSpeedKmh ?? 0) : 0;
            var maxRain = taskPoints.Sum(p => p.PrecipitationMm ?? 0);

            if (maxWind >= 18.0 || maxRain >= 1.0)
            {
                // Look for alternative calm date in next 5 days
                DateOnly? bestAlternativeDate = null;
                var testDate = today.AddDays(1);
                while (testDate <= today.AddDays(6))
                {
                    if (testDate != taskDate)
                    {
                        var dayPoints = points.Where(p => DateOnly.FromDateTime(p.ObservedAt) == testDate).ToList();
                        var dayWind = dayPoints.Count > 0 ? dayPoints.Max(p => p.WindSpeedKmh ?? 0) : 0;
                        var dayRain = dayPoints.Sum(p => p.PrecipitationMm ?? 0);
                        if (dayWind <= 12.0 && dayRain == 0)
                        {
                            bestAlternativeDate = testDate;
                            break;
                        }
                    }
                    testDate = testDate.AddDays(1);
                }

                var dedupeKey = $"adv-spray-{farm.Id}-{task.Id}-{taskDate:yyyyMMdd}";
                var metrics = JsonSerializer.Serialize(new
                {
                    wind_speed_kmh = maxWind,
                    rain_mm = maxRain,
                    alternative_date = bestAlternativeDate?.ToString("yyyy-MM-dd")
                });

                results.Add(new ProactiveAdvisoryEvaluationResult(
                    AdvisoryType: ProactiveAdvisoryType.SprayingWindow,
                    Severity: AdvisorySeverity.Warning,
                    ActionType: ProactiveActionType.RescheduleSpraying,
                    Title: "Rüzgar & Yağmur Uyarısı: İlaçlama Sürüklenme Riski",
                    Summary: $"İlaçlama planlanan günde rüzgar hızı {maxWind:F0} km/s seviyesine çıkacaktır. İlacın hedeften sapmaması (sürüklenme/drift) için uygun pencereye erteleyin.",
                    AgronomicExplanation: $"18 km/s üzerindeki rüzgar hızlarında pülverizatör damlacıkları çevre alanlara ve komşu tarlalara sürüklenir, hedef bitkide yeterli biyolojik etkinlik sağlanamaz. İlaçlama için rüzgarın 12 km/s altına indiği saatler tercih edilmelidir.",
                    ActionRecommendation: bestAlternativeDate.HasValue
                        ? $"İlaçlamayı rüzgarın sakin olacağı {bestAlternativeDate.Value:d MMMM dddd} sabahına planlayın."
                        : "İlaçlama gününü rüzgarın 12 km/s altına düşeceği sakin bir zamana erteleyin.",
                    RecommendedDate: bestAlternativeDate,
                    DedupeKey: dedupeKey,
                    RelatedTaskId: task.Id,
                    MetricsJson: metrics));
            }
        }
    }

    private static void EvaluateFrostAlert(
        Farm farm,
        List<WeatherPoint> points,
        List<ProactiveAdvisoryEvaluationResult> results)
    {
        var freezingPoint = points
            .Where(p => p.TemperatureC.HasValue && p.TemperatureC.Value <= 0.5)
            .OrderBy(p => p.TemperatureC!.Value)
            .FirstOrDefault();

        if (freezingPoint != null)
        {
            var frostDate = DateOnly.FromDateTime(freezingPoint.ObservedAt);
            var minTemp = freezingPoint.TemperatureC!.Value;

            var dedupeKey = $"adv-frost-{farm.Id}-{frostDate:yyyyMMdd}";
            var metrics = JsonSerializer.Serialize(new
            {
                min_temperature_c = minTemp,
                frost_time = freezingPoint.ObservedAt.ToString("yyyy-MM-dd HH:mm UTC")
            });

            results.Add(new ProactiveAdvisoryEvaluationResult(
                AdvisoryType: ProactiveAdvisoryType.FrostAlert,
                Severity: AdvisorySeverity.Critical,
                ActionType: ProactiveActionType.EmergencyProtection,
                Title: "Kritik Don Uyarısı: Gece Sıcaklığı Düşüyor",
                Summary: $"{frostDate:d MMMM} gecesi sıcaklığın {minTemp:F1}°C seviyesine düşmesi bekleniyor. Don ve soğuk zararına karşı koruma tedbirleri alın.",
                AgronomicExplanation: $"Sıcaklığın sıfırın altına inmesi hassas bitki dokularındaki hücre içi suyun donmasına ve hücre çeperlerinin parçalanmasına neden olur. Özellikle ilkbahar geç donları çiçek ve taze sürgünlerde geri dönüşsüz hasar bırakır.",
                ActionRecommendation: "Dumanlama, rüzgar pervanesi veya mini yağmurlama sistemlerinizi gece saatleri için hazır bulundurun.",
                RecommendedDate: frostDate,
                DedupeKey: dedupeKey,
                RelatedTaskId: null,
                MetricsJson: metrics));
        }
    }

    private static void EvaluateFungalDiseaseRisk(
        Farm farm,
        List<WeatherPoint> points,
        DateTime nowUtc,
        List<ProactiveAdvisoryEvaluationResult> results)
    {
        // Check for consecutive hours of humidity >= 80% and temp 17-26C
        int consecutiveHours = 0;
        foreach (var p in points.Where(p => p.ObservedAt >= nowUtc).OrderBy(p => p.ObservedAt))
        {
            var humidity = p.HumidityPercent ?? 0;
            var temp = p.TemperatureC ?? 0;

            if (humidity >= 80 && temp is >= 17 and <= 26)
            {
                consecutiveHours++;
            }
            else
            {
                consecutiveHours = 0;
            }

            if (consecutiveHours >= 20)
            {
                var dedupeKey = $"adv-fungal-{farm.Id}-{DateOnly.FromDateTime(nowUtc):yyyyMMdd}";
                var metrics = JsonSerializer.Serialize(new
                {
                    consecutive_humid_hours = consecutiveHours,
                    average_temp_c = temp,
                    humidity_pct = humidity
                });

                results.Add(new ProactiveAdvisoryEvaluationResult(
                    AdvisoryType: ProactiveAdvisoryType.FungalDiseaseRisk,
                    Severity: AdvisorySeverity.Warning,
                    ActionType: ProactiveActionType.FieldScouting,
                    Title: "Mantari Hastalık Riski: Ilık ve Nemli Hava",
                    Summary: "Son 24 saatteki yüksek nem ve ılık hava koşulları mantari hastalık (mildiyö/yaprak lekesi) sporlanmasını tetiklemektedir.",
                    AgronomicExplanation: "Yaprak yüzeyinde 12 saati aşan serbest nem ve 18-24°C sıcaklık, pas, külleme ve mildiyö mantarlarının konukçu dokusuna nüfuz etmesi (çimlenme) için ideal ortamı yaratır.",
                    ActionRecommendation: "Tarlanızı yaprak altı lekeleri ve spor belirtileri yönünden kontrol (scouting) edin, gerekirse koruyucu fungisit uygulayın.",
                    RecommendedDate: DateOnly.FromDateTime(nowUtc).AddDays(1),
                    DedupeKey: dedupeKey,
                    RelatedTaskId: null,
                    MetricsJson: metrics));
                break;
            }
        }
    }

    private static readonly System.Globalization.CultureInfo TurkishCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    private static bool IsFertilizerTask(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLower(TurkishCulture);
        return text.Contains("gübre") || text.Contains("fertiliz") || text.Contains("üre") || text.Contains("azot");
    }

    private static bool IsIrrigationTask(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLower(TurkishCulture);
        return text.Contains("sulama") || text.Contains("irrigat") || text.Contains("damla");
    }

    private static bool IsSprayingTask(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLower(TurkishCulture);
        return text.Contains("ilaç") || text.Contains("ilac") || text.Contains("spray") || text.Contains("fungisit") || text.Contains("insektisit") || text.Contains("herbisit");
    }
}
