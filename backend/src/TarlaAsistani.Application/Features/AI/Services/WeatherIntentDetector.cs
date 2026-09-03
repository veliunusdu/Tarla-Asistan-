using System.Globalization;
using System.Text.RegularExpressions;

namespace TarlaAsistani.Application.Features.AI.Services;

/// <summary>
/// Determines whether a user message is weather-relevant.
/// Deterministic, no external calls. Turkish-aware keyword matching.
/// Deliberately kept simple and maintainable — not a giant keyword list.
/// </summary>
public static class WeatherIntentDetector
{
    // ── Primary weather terms ─────────────────────────────────────────────
    private static readonly string[] WeatherKeywords =
    {
        // Weather concepts
        "hava", "hava durumu", "yağmur", "yağış", "yağmur", "kar", "fırtına",
        "rüzgar", "rüzgâr", "sıcaklık", "sıcak", "soğuk", "don", "dolu",
        "nem", "bulut", "bulutlu", "güneş", "güneşli", "sis", "dolu",

        // Time references (weather-relevant)
        "bugün", "yarın", "öbür gün", "hafta", "bu hafta", "gelecek hafta",
        "sabah", "öğle", "akşam", "gece", "gündüz",

        // Weather-sensitive farm operations
        "sulama", "sula", "sulandım", "ilaçlama", "ilaçla", "ilaç",
        "gübreleme", "gübre", "ekim", "ek", "hasat", "biç",
        "iş planı", "günlük plan", "ne yapayım", "ne yapacağım",
        "tarlada", "çalışma", "plan",

        // Risk terms
        "risk", "tehlike", "uyarı",
    };

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CompareInfo TurkishCompare = TurkishCulture.CompareInfo;
    private static readonly Regex WordRegex = new(@"[\p{L}\p{M}]+", RegexOptions.Compiled);
    private static readonly IReadOnlyList<string[]> WeatherKeywordTokens =
        WeatherKeywords.Select(Tokenize).Where(tokens => tokens.Length > 0).ToList();

    /// <summary>
    /// Returns true if the message appears to be weather-relevant.
    /// Uses whole-word matching with Turkish culture-aware case comparison.
    /// </summary>
    public static bool IsWeatherRelevant(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var tokens = Tokenize(message);
        foreach (var keywordTokens in WeatherKeywordTokens)
        {
            if (ContainsTokenSequence(tokens, keywordTokens))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to find a single farm name that matches a farm name mentioned in the message.
    /// Returns the matched farm id, or null if zero or multiple farms match.
    /// Case-insensitive and Turkish-safe without transforming the source text.
    /// </summary>
    public static Guid? TryMatchFarmByName(
        string message,
        IEnumerable<(Guid Id, string Name)> farms)
    {
        var messageTokens = Tokenize(message);

        Guid? matched = null;
        var matchCount = 0;

        foreach (var (id, name) in farms)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (ContainsTokenSequence(
                    messageTokens,
                    Tokenize(name),
                    allowLastTokenSuffix: true))
            {
                matched = id;
                matchCount++;
            }
        }

        return matchCount == 1 ? matched : null;
    }

    private static string[] Tokenize(string value) =>
        WordRegex.Matches(value).Select(match => match.Value).ToArray();

    private static bool ContainsTokenSequence(
        IReadOnlyList<string> source,
        IReadOnlyList<string> candidate,
        bool allowLastTokenSuffix = false)
    {
        if (candidate.Count == 0 || candidate.Count > source.Count)
            return false;

        for (var start = 0; start <= source.Count - candidate.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < candidate.Count; offset++)
            {
                var sourceToken = source[start + offset];
                var candidateToken = candidate[offset];
                var isLastFarmNameToken = allowLastTokenSuffix && offset == candidate.Count - 1;
                var tokenMatches = TurkishCompare.Compare(
                    sourceToken,
                    candidateToken,
                    CompareOptions.IgnoreCase) == 0;

                // Turkish case endings are attached to the final farm-name word,
                // for example "Kuzey Tarla" -> "Kuzey tarlada".
                if (!tokenMatches && isLastFarmNameToken)
                    tokenMatches = TurkishCompare.IsPrefix(
                        sourceToken,
                        candidateToken,
                        CompareOptions.IgnoreCase);

                if (!tokenMatches)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }
}
