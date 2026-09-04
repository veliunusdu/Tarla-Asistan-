using System.Globalization;
using System.Xml;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Türkiye Cumhuriyet Merkez Bankası (TCMB) resmi günlük kur bülteninden (today.xml)
/// döviz kurlarını (USD/TRY, EUR/TRY) çeken sağlayıcı.
/// </summary>
public class TcmbMarketDataProvider : IMarketDataProvider
{
    private const string TcmbXmlUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";
    private readonly HttpClient _httpClient;
    private readonly ILogger<TcmbMarketDataProvider> _logger;

    public TcmbMarketDataProvider(
        HttpClient httpClient,
        ILogger<TcmbMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(MarketCategory category)
    {
        return Task.FromResult(category == MarketCategory.Fx);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MarketPrice>> FetchAsync(MarketCategory category, CancellationToken ct = default)
    {
        if (category != MarketCategory.Fx)
        {
            return [];
        }

        try
        {
            _logger.LogInformation("TCMB döviz kurları {Url} adresinden çekiliyor...", TcmbXmlUrl);

            using var response = await _httpClient.GetAsync(TcmbXmlUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TCMB kur bülteni indirilemedi. HTTP Durum Kodu: {StatusCode}", response.StatusCode);
                return [];
            }

            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                _logger.LogWarning("TCMB kur yanıtı boş içerik döndürdü.");
                return [];
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);

            var prices = new List<MarketPrice>();
            var now = DateTime.UtcNow;

            // USD/TRY
            var usdNode = xmlDoc.SelectSingleNode("//Currency[@Kod='USD']/ForexBuying");
            if (usdNode != null && decimal.TryParse(usdNode.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out var usdRate))
            {
                prices.Add(new MarketPrice
                {
                    Code = "USD_TRY",
                    Name = "Dolar",
                    Category = MarketCategory.Fx,
                    CurrentPrice = usdRate,
                    Unit = "TL",
                    Source = "TCMB",
                    UpdatedAtUtc = now
                });
            }
            else
            {
                _logger.LogWarning("TCMB XML içeriğinde USD ForexBuying düğümü bulunamadı veya çözümlenemedi.");
            }

            // EUR/TRY
            var eurNode = xmlDoc.SelectSingleNode("//Currency[@Kod='EUR']/ForexBuying");
            if (eurNode != null && decimal.TryParse(eurNode.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out var eurRate))
            {
                prices.Add(new MarketPrice
                {
                    Code = "EUR_TRY",
                    Name = "Euro",
                    Category = MarketCategory.Fx,
                    CurrentPrice = eurRate,
                    Unit = "TL",
                    Source = "TCMB",
                    UpdatedAtUtc = now
                });
            }
            else
            {
                _logger.LogWarning("TCMB XML içeriğinde EUR ForexBuying düğümü bulunamadı veya çözümlenemedi.");
            }

            _logger.LogInformation("TCMB üzerinden {Count} adet döviz kuru başarıyla alındı.", prices.Count);
            return prices;
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or TaskCanceledException)
        {
            _logger.LogError(ex, "TCMB kurları çekilirken veya XML ayrıştırılırken hata oluştu.");
            return [];
        }
    }
}
