using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Features.Market.DTOs;

/// <summary>
/// Piyasa verileri API yanıtını ve son güncellenme zamanını sarmalayan yanıt nesnesi.
/// </summary>
public class MarketDataResponseDto
{
    /// <summary>
    /// Listedeki veriler arasındaki en son güncellenme zamanı (UTC).
    /// </summary>
    [JsonPropertyName("last_updated_utc")]
    public DateTime LastUpdatedUtc { get; init; }

    /// <summary>
    /// Piyasa kalemlerinin listesi.
    /// </summary>
    [JsonPropertyName("items")]
    public List<MarketItemDto> Items { get; init; } = [];
}
