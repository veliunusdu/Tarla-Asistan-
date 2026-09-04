using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Akaryakıt, gübre ve mahsul kategorilerinde canlı API entegrasyonu bulunmayan ürünler için
/// yapılandırılabilir (override) veya varsayılan piyasa verilerini sağlayan MVP sağlayıcısı.
/// </summary>
public class StaticMarketDataProvider : IMarketDataProvider
{
    private readonly StaticMarketDataOptions _options;
    private readonly ILogger<StaticMarketDataProvider> _logger;

    public StaticMarketDataProvider(
        IOptions<StaticMarketDataOptions> options,
        ILogger<StaticMarketDataProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(MarketCategory category)
    {
        return Task.FromResult(
            category is MarketCategory.Fuel
                     or MarketCategory.Fertilizer
                     or MarketCategory.Crop);
    }

    /// <inheritdoc />
    public Task<IEnumerable<MarketPrice>> FetchAsync(MarketCategory category, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var items = new List<MarketPrice>();

        switch (category)
        {
            case MarketCategory.Fuel:
                items.Add(CreateItem("DIESEL", "Motorin (Mazot)", MarketCategory.Fuel, 45.10m, "TL/Lt", "EPDK", now));
                items.Add(CreateItem("GASOLINE", "Benzin (95 Oktan)", MarketCategory.Fuel, 43.45m, "TL/Lt", "EPDK", now));
                break;

            case MarketCategory.Fertilizer:
                items.Add(CreateItem("UREA", "Üre Gübresi (%46 N)", MarketCategory.Fertilizer, 14350.00m, "TL/Ton", "GUBRETAS", now));
                items.Add(CreateItem("DAP", "DAP Gübresi (18-46-0)", MarketCategory.Fertilizer, 20900.00m, "TL/Ton", "GUBRETAS", now));
                break;

            case MarketCategory.Crop:
                items.Add(CreateItem("WHEAT", "Ekmeklik Buğday", MarketCategory.Crop, 9900.00m, "TL/Ton", "TURIB", now));
                items.Add(CreateItem("CORN", "Mısır (1. Sınıf)", MarketCategory.Crop, 8250.00m, "TL/Ton", "TURIB", now));
                break;
        }

        return Task.FromResult<IEnumerable<MarketPrice>>(items);
    }

    private MarketPrice CreateItem(
        string code,
        string name,
        MarketCategory category,
        decimal defaultPrice,
        string unit,
        string source,
        DateTime now)
    {
        decimal price;

        if (_options.PriceOverrides.TryGetValue(code, out var overridePrice))
        {
            price = overridePrice;
            _logger.LogInformation("Piyasa fiyatı için yapılandırma geçersiz kılması (override) uygulandı: {Code} = {Price}", code, price);
        }
        else
        {
            price = defaultPrice;
            _logger.LogDebug("Piyasa fiyatı için varsayılan referans değer kullanıldı: {Code} = {Price}", code, price);
        }

        return new MarketPrice
        {
            Code = code,
            Name = name,
            Category = category,
            CurrentPrice = price,
            Unit = unit,
            Source = source,
            UpdatedAtUtc = now
        };
    }
}
