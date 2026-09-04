using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Features.Market.DTOs;

/// <summary>
/// Mobil istemciye ve web paneline sunulan tekil piyasa kalemi (akaryakıt, gübre, mahsul, döviz).
/// </summary>
public class MarketItemDto
{
    /// <summary>
    /// Kalemin tekil sistem kodu (örn: "DIESEL", "USD_TRY").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = null!;

    /// <summary>
    /// Görüntülenecek ürün veya parite adı (örn: "Motorin (Mazot)", "Dolar").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Kategori adı (küçük harf: "fuel", "fertilizer", "crop", "fx").
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = null!;

    /// <summary>
    /// Güncel birim fiyat (TL).
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    /// <summary>
    /// Önceki günün referans/kapanış fiyatı (TL).
    /// </summary>
    [JsonPropertyName("previous_price")]
    public decimal PreviousPrice { get; init; }

    /// <summary>
    /// Günlük değişim yüzdesi (örn: 1.47, -0.80).
    /// </summary>
    [JsonPropertyName("change_percent")]
    public decimal ChangePercent { get; init; }

    /// <summary>
    /// Fiyat değişim yönü ("up", "down", "neutral").
    /// </summary>
    [JsonPropertyName("change_direction")]
    public string ChangeDirection { get; init; } = null!;

    /// <summary>
    /// Fiyatlandırma birimi (örn: "TL/Lt", "TL/Ton", "TL").
    /// </summary>
    [JsonPropertyName("unit")]
    public string Unit { get; init; } = null!;

    /// <summary>
    /// Mobil arayüzde simge eşleştirmesi için anahtar (örn: "fuel_diesel", "fx_usd_try").
    /// </summary>
    [JsonPropertyName("icon_key")]
    public string IconKey { get; init; } = null!;

    /// <summary>
    /// Fiyat verisinin son güncellenme zamanı (UTC).
    /// </summary>
    [JsonPropertyName("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; init; }
}
