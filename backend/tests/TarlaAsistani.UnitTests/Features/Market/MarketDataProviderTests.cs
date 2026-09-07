using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Features.Market;

public class MarketDataProviderTests
{
    [Fact]
    public async Task StaticProvider_marks_prices_as_reference_data()
    {
        var provider = new StaticMarketDataProvider(
            Options.Create(new StaticMarketDataOptions()),
            NullLogger<StaticMarketDataProvider>.Instance);

        var prices = (await provider.FetchAsync(MarketCategory.Crop)).ToList();

        prices.Should().NotBeEmpty();
        prices.Should().OnlyContain(price => price.PriceType == "reference");
    }

    [Fact]
    public async Task TcmbProvider_marks_prices_as_live_data()
    {
        using var httpClient = new HttpClient(new FakeTcmbHandler());
        var provider = new TcmbMarketDataProvider(
            httpClient,
            NullLogger<TcmbMarketDataProvider>.Instance);

        var prices = (await provider.FetchAsync(MarketCategory.Fx)).ToList();

        prices.Should().HaveCount(2);
        prices.Should().OnlyContain(price => price.PriceType == "live");
    }

    private sealed class FakeTcmbHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string xml = """
                <Tarih_Date Tarih="07.09.2026" Date="09/07/2026">
                  <Currency Kod="USD"><ForexBuying>41.20</ForexBuying></Currency>
                  <Currency Kod="EUR"><ForexBuying>48.10</ForexBuying></Currency>
                </Tarih_Date>
                """;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(xml)
            });
        }
    }
}
