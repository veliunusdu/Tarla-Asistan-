using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Features.Market;

public class EpdkFuelMarketDataProviderTests
{
    [Fact]
    public async Task FetchAsync_maps_epdk_fuel_prices_as_live_data()
    {
        using var client = new HttpClient(new FakeHandler(SuccessEnvelope));
        var provider = new EpdkFuelMarketDataProvider(
            client,
            NullLogger<EpdkFuelMarketDataProvider>.Instance);

        var prices = (await provider.FetchAsync(MarketCategory.Fuel)).ToList();

        prices.Should().HaveCount(2);
        prices.Should().ContainSingle(p => p.Code == "DIESEL" && p.CurrentPrice == 52.35m);
        prices.Should().ContainSingle(p => p.Code == "GASOLINE" && p.CurrentPrice == 54.10m);
        prices.Should().OnlyContain(p => p.PriceType == "live" && p.Source == "EPDK");
    }

    [Fact]
    public async Task CanHandleAsync_supports_only_fuel()
    {
        using var client = new HttpClient(new FakeHandler(SuccessEnvelope));
        var provider = new EpdkFuelMarketDataProvider(
            client,
            NullLogger<EpdkFuelMarketDataProvider>.Instance);

        (await provider.CanHandleAsync(MarketCategory.Fuel)).Should().BeTrue();
        (await provider.CanHandleAsync(MarketCategory.Crop)).Should().BeFalse();
    }

    [Fact]
    public async Task FetchAsync_returns_empty_when_epdk_is_unavailable()
    {
        using var client = new HttpClient(new FakeHandler("service unavailable", System.Net.HttpStatusCode.ServiceUnavailable));
        var provider = new EpdkFuelMarketDataProvider(
            client,
            NullLogger<EpdkFuelMarketDataProvider>.Instance);

        var prices = await provider.FetchAsync(MarketCategory.Fuel);

        prices.Should().BeEmpty();
    }

    private const string SuccessEnvelope = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <genelSorguResponse xmlns="http://genel.service.ws.epvys.g222.tubitak.gov.tr/">
              <return><![CDATA[
                <PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlariResult>
                  <PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlari>
                    <YakitTipi>Motorin</YakitTipi><Birim>Litre</Birim><Fiyat>52.35</Fiyat>
                  </PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlari>
                  <PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlari>
                    <YakitTipi>Kurşunsuz Benzin 95 Oktan</YakitTipi><Birim>Litre</Birim><Fiyat>54.10</Fiyat>
                  </PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlari>
                </PetrolPiyasasiEnYuksekHacimliSekizFirmaninAkaryakitFiyatlariResult>
              ]]></return>
            </genelSorguResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly System.Net.HttpStatusCode _statusCode;

        public FakeHandler(string body, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _body = body;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            });
    }
}
