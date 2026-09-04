namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Statik veya yapılandırılmış piyasa verisi sağlayıcısı için seçenekler sınıfı.
/// </summary>
public class StaticMarketDataOptions
{
    /// <summary>
    /// appsettings.json veya ortam değişkenleri üzerinden belirli ürün kodlarına uygulanan fiyat geçersiz kılmaları.
    /// Anahtar: Ürün kodu (örn: "DIESEL", "GASOLINE"), Değer: Yeni birim fiyatı.
    /// </summary>
    public Dictionary<string, decimal> PriceOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Verilerin yeniden sorgulanmadan önce önbellekte tutulacağı varsayılan süre (varsayılan: 8 saat).
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(8);
}
