using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// EPDK'nın yüksek işlem hacimli sekiz firmanın ortalama akaryakıt bülteninden
/// motorin ve kurşunsuz benzin fiyatlarını okur.
/// </summary>
public sealed class EpdkFuelMarketDataProvider : IMarketDataProvider
{
    private const string DefaultEndpoint =
        "https://lisansws.epdk.gov.tr/services/bildirimPetrol8FirmaBulten.bildirimPetrol8FirmaBultenHttpSoap11Endpoint";
    private const string Namespace = "http://genel.service.ws.epvys.g222.tubitak.gov.tr/";
    private readonly HttpClient _httpClient;
    private readonly ILogger<EpdkFuelMarketDataProvider> _logger;

    public EpdkFuelMarketDataProvider(
        HttpClient httpClient,
        ILogger<EpdkFuelMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public Task<bool> CanHandleAsync(MarketCategory category) =>
        Task.FromResult(category == MarketCategory.Fuel);

    public async Task<IEnumerable<MarketPrice>> FetchAsync(
        MarketCategory category,
        CancellationToken ct = default)
    {
        if (category != MarketCategory.Fuel) return [];

        var requestDate = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var envelope = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:gen="{Namespace}">
              <soapenv:Header/>
              <soapenv:Body>
                <gen:genelSorgu>
                  <sorguNo>71</sorguNo>
                  <parametreler>{requestDate}</parametreler>
                </gen:genelSorgu>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "genelSorgu");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EPDK akaryakıt servisi HTTP {StatusCode} döndürdü.", response.StatusCode);
                return [];
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(xml);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "EPDK akaryakıt verisi alınamadı; mevcut veri korunacak.");
            return [];
        }
    }

    public static IReadOnlyList<MarketPrice> ParseResponse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);

        var returnNode = document.SelectSingleNode("//*[local-name()='return']");
        var payload = returnNode?.InnerText;
        if (string.IsNullOrWhiteSpace(payload)) return [];

        var result = new XmlDocument();
        result.LoadXml(payload);
        var prices = new List<MarketPrice>();
        var itemNodes = result.SelectNodes(
            "//*[local-name()='PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlari']");

        if (itemNodes is null) return prices;

        foreach (XmlNode item in itemNodes)
        {
            var fuelName = ChildText(item, "YakitTipi");
            var priceText = ChildText(item, "Fiyat");
            if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
                continue;

            var normalizedName = Normalize(fuelName);
            var (code, name) = normalizedName.Contains("motorin", StringComparison.Ordinal)
                ? ("DIESEL", "Motorin (Mazot)")
                : normalizedName.Contains("benzin 95", StringComparison.Ordinal)
                    ? ("GASOLINE", "Benzin (95 Oktan)")
                    : (null, null);

            if (code == null || prices.Any(p => p.Code == code)) continue;

            prices.Add(new MarketPrice
            {
                Code = code,
                Name = name!,
                Category = MarketCategory.Fuel,
                CurrentPrice = price,
                Unit = "TL/Lt",
                Source = "EPDK",
                PriceType = "live",
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        return prices;
    }

    private static string ChildText(XmlNode node, string localName) =>
        node.SelectSingleNode($"*[local-name()='{localName}']")?.InnerText.Trim() ?? string.Empty;

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
