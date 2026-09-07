using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// TÜRİB'in günlük bülteninden tanınan hububatların ağırlıklı ortalama fiyatlarını çeker.
/// Bülten kullanılamazsa boş sonuç döndürür; böylece mevcut referans fiyat korunur.
/// </summary>
public sealed class TuribCropMarketDataProvider : IMarketDataProvider
{
    private const string BulletinUrl = "https://api.turib.com.tr/api/getbulletin/{0}";
    private readonly HttpClient _httpClient;
    private readonly ILogger<TuribCropMarketDataProvider> _logger;

    public TuribCropMarketDataProvider(
        HttpClient httpClient,
        ILogger<TuribCropMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _logger = logger;
    }

    public Task<bool> CanHandleAsync(MarketCategory category) =>
        Task.FromResult(category == MarketCategory.Crop);

    public async Task<IEnumerable<MarketPrice>> FetchAsync(
        MarketCategory category,
        CancellationToken ct = default)
    {
        if (category != MarketCategory.Crop)
        {
            return [];
        }

        var url = string.Format(
            CultureInfo.InvariantCulture,
            BulletinUrl,
            DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TÜRİB bülteni alınamadı. HTTP durum kodu: {StatusCode}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "TÜRİB bülten servisine erişilemedi.");
            return [];
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "TÜRİB bülten isteği zaman aşımına uğradı.");
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "TÜRİB bülten yanıtı çözümlenemedi.");
            return [];
        }
    }

    internal static IEnumerable<MarketPrice> ParseResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
            : GetDailyRows(document.RootElement);

        var result = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !TryGetString(row, out var productName, "ProductName", "UrunAdi", "Product", "Name") ||
                !TryGetPrice(row, out var price, "AOF", "VWAP", "WeightedAveragePrice", "AgirlikliOrtalamaFiyat", "ClosingPrice", "KapanisFiyati"))
            {
                continue;
            }

            var code = NormalizeProductCode(productName);
            if (code is null || result.ContainsKey(code))
            {
                continue;
            }

            result[code] = new MarketPrice
            {
                Code = code,
                Name = code == "WHEAT" ? "Ekmeklik Buğday" : "Mısır (1. Sınıf)",
                Category = MarketCategory.Crop,
                CurrentPrice = price,
                Unit = "TL/Ton",
                Source = "TURIB",
                PriceType = "live",
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        return result.Values;
    }

    private static IEnumerable<JsonElement> GetDailyRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            TryGetProperty(root, out var dailyList, "DailyList") &&
            dailyList.ValueKind == JsonValueKind.Array)
        {
            return dailyList.EnumerateArray();
        }

        return [];
    }

    private static string? NormalizeProductCode(string value)
    {
        var normalized = RemoveDiacritics(value).ToLowerInvariant();
        if (normalized.Contains("bugday") &&
            (normalized.Contains("ekmeklik") || normalized == "bugday"))
        {
            return "WHEAT";
        }

        if (normalized.Contains("misir"))
        {
            return "CORN";
        }

        return null;
    }

    private static bool TryGetPrice(JsonElement row, out decimal price, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(row, out var value, name))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out price))
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out price))
            {
                return true;
            }
        }

        price = default;
        return false;
    }

    private static bool TryGetString(JsonElement row, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(row, out var property, name) && property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString()
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Normalize(NormalizationForm.FormC);
    }
}
