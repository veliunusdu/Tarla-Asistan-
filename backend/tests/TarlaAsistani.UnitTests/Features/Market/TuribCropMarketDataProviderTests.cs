using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Features.Market;

public class TuribCropMarketDataProviderTests
{
    [Fact]
    public async Task FetchAsync_maps_daily_bulletin_crop_prices_as_live_data()
    {
        using var client = new HttpClient(new FakeHandler(SuccessResponse));
        var provider = new TuribCropMarketDataProvider(
            client,
            NullLogger<TuribCropMarketDataProvider>.Instance);

        var prices = (await provider.FetchAsync(MarketCategory.Crop)).ToList();

        prices.Should().HaveCount(2);
        prices.Should().ContainSingle(p => p.Code == "WHEAT" && p.CurrentPrice == 10125.50m);
        prices.Should().ContainSingle(p => p.Code == "CORN" && p.CurrentPrice == 8650.00m);
        prices.Should().OnlyContain(p => p.PriceType == "live" && p.Source == "TURIB");
    }

    [Fact]
    public async Task CanHandleAsync_supports_only_crop()
    {
        using var client = new HttpClient(new FakeHandler(SuccessResponse));
        var provider = new TuribCropMarketDataProvider(
            client,
            NullLogger<TuribCropMarketDataProvider>.Instance);

        (await provider.CanHandleAsync(MarketCategory.Crop)).Should().BeTrue();
        (await provider.CanHandleAsync(MarketCategory.Fuel)).Should().BeFalse();
    }

    [Fact]
    public async Task FetchAsync_returns_empty_when_bulletin_has_no_tradable_prices()
    {
        using var client = new HttpClient(new FakeHandler("{\"DailyList\":[]}"));
        var provider = new TuribCropMarketDataProvider(
            client,
            NullLogger<TuribCropMarketDataProvider>.Instance);

        (await provider.FetchAsync(MarketCategory.Crop)).Should().BeEmpty();
    }

    private const string SuccessResponse = """
        {
          "DailyList": [
            { "ProductName": "Buğday Ekmeklik", "AOF": 10125.50, "TradeDate": "2026-09-07" },
            { "ProductName": "Mısır", "VWAP": 8650.00, "TradeDate": "2026-09-07" },
            { "ProductName": "Arpa", "AOF": 7800.00, "TradeDate": "2026-09-07" }
          ]
        }
        """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;

        public FakeHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
    }
}
