using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Market.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Market.Queries;

/// <summary>
/// Piyasa verilerini (opsiyonel kategori filtresi ile) getiren MediatR sorgusu.
/// </summary>
public record GetMarketDataQuery(MarketCategory? Category = null) : IRequest<MarketDataResponseDto>;

/// <summary>
/// <see cref="GetMarketDataQuery"/> sorgusunu işleyen, 15 dakikalık bellek içi (IMemoryCache) önbellekleme uygulayan işleyici.
/// </summary>
public class GetMarketDataQueryHandler(
    IApplicationDbContext dbContext,
    IMemoryCache memoryCache) : IRequestHandler<GetMarketDataQuery, MarketDataResponseDto>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<MarketDataResponseDto> Handle(GetMarketDataQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = request.Category.HasValue
            ? $"market_data_{request.Category.Value.ToString().ToLowerInvariant()}"
            : "market_data_all";

        if (memoryCache.TryGetValue(cacheKey, out MarketDataResponseDto? cachedDto) && cachedDto is not null)
        {
            return cachedDto;
        }

        var query = dbContext.MarketPrices.AsNoTracking();

        if (request.Category.HasValue)
        {
            query = query.Where(x => x.Category == request.Category.Value);
        }

        var entities = await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MapToDto).ToList();

        var lastUpdated = items.Count > 0
            ? items.Max(x => x.UpdatedAtUtc)
            : DateTime.UtcNow;

        var response = new MarketDataResponseDto
        {
            LastUpdatedUtc = lastUpdated,
            Items = items
        };

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        };

        memoryCache.Set(cacheKey, response, cacheOptions);

        return response;
    }

    private static MarketItemDto MapToDto(MarketPrice entity) => new()
    {
        Code = entity.Code,
        Name = entity.Name,
        Category = entity.Category.ToString().ToLowerInvariant(),
        Price = entity.CurrentPrice,
        PreviousPrice = entity.PreviousPrice,
        ChangePercent = entity.ChangePercent,
        ChangeDirection = entity.ChangeDirection,
        Unit = entity.Unit,
        IconKey = GenerateIconKey(entity.Category, entity.Code),
        UpdatedAtUtc = entity.UpdatedAtUtc
    };

    /// <summary>
    /// Kategori ve ürün kodundan mobil uyumlu simge anahtarı üretir (örn: DIESEL -> "fuel_diesel").
    /// </summary>
    public static string GenerateIconKey(MarketCategory category, string code)
    {
        var categoryPrefix = category switch
        {
            MarketCategory.Fuel => "fuel",
            MarketCategory.Fertilizer => "fertilizer",
            MarketCategory.Crop => "crop",
            MarketCategory.Fx => "fx",
            _ => "market"
        };

        var normalizedCode = code.Trim().ToLowerInvariant();
        return $"{categoryPrefix}_{normalizedCode}";
    }
}
