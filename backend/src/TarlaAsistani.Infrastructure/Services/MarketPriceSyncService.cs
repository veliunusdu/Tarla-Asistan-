using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

/// <summary>
/// Harici sağlayıcılardan piyasa fiyatlarını çeken, önceki fiyat farklarını ve değişim yüzdelerini hesaplayan
/// ve veritabanı ile önbelleği güncelleyen temel iş mantığı servisi.
/// </summary>
public class MarketPriceSyncService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEnumerable<IMarketDataProvider> _providers;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MarketPriceSyncService> _logger;

    public MarketPriceSyncService(
        IApplicationDbContext dbContext,
        IEnumerable<IMarketDataProvider> providers,
        IMemoryCache memoryCache,
        ILogger<MarketPriceSyncService> logger)
    {
        _dbContext = dbContext;
        _providers = providers;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Belirtilen kategoriye ait fiyatları uygun sağlayıcıdan çekip veritabanına ve önbelleğe yansıtır.
    /// </summary>
    /// <param name="category">Senkronize edilecek kategori.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Güncellenen veya eklenen kayıt sayısı.</returns>
    public async Task<int> SyncAsync(MarketCategory category, CancellationToken ct = default)
    {
        _logger.LogInformation("{Category} kategorisi için piyasa verisi senkronizasyonu başlatılıyor...", category);

        IMarketDataProvider? matchedProvider = null;

        foreach (var provider in _providers)
        {
            if (await provider.CanHandleAsync(category))
            {
                matchedProvider = provider;
                break;
            }
        }

        if (matchedProvider is null)
        {
            _logger.LogWarning("{Category} kategorisini işleyecek uygun bir IMarketDataProvider bulunamadı.", category);
            return 0;
        }

        var freshPrices = (await matchedProvider.FetchAsync(category, ct)).ToList();
        if (freshPrices.Count == 0)
        {
            _logger.LogWarning("Sağlayıcı ({Provider}) {Category} kategorisi için hiçbir veri döndürmedi.",
                matchedProvider.GetType().Name, category);
            return 0;
        }

        var codes = freshPrices.Select(p => p.Code).ToList();

        using var transaction = await _dbContext.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var affectedCount = 0;

        try
        {
            var existingEntities = await _dbContext.MarketPrices
                .Where(x => codes.Contains(x.Code))
                .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var fresh in freshPrices)
            {
                if (existingEntities.TryGetValue(fresh.Code, out var existing))
                {
                    existing.PreviousPrice = existing.CurrentPrice;
                    existing.CurrentPrice = fresh.CurrentPrice;
                    existing.Name = fresh.Name;
                    existing.Unit = fresh.Unit;
                    existing.Source = fresh.Source;
                    existing.UpdatedAtUtc = now;

                    if (existing.PreviousPrice > 0)
                    {
                        var rawChange = ((existing.CurrentPrice - existing.PreviousPrice) / existing.PreviousPrice) * 100m;
                        existing.ChangePercent = Math.Round(rawChange, 2);
                    }
                    else
                    {
                        existing.ChangePercent = 0m;
                    }

                    affectedCount++;
                }
                else
                {
                    fresh.PreviousPrice = fresh.CurrentPrice;
                    fresh.ChangePercent = 0m;
                    fresh.UpdatedAtUtc = now;

                    await _dbContext.MarketPrices.AddAsync(fresh, ct);
                    affectedCount++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Önbellek anahtarlarını geçersiz kıl
            _memoryCache.Remove("market_data_all");
            _memoryCache.Remove($"market_data_{category.ToString().ToLowerInvariant()}");

            _logger.LogInformation("{Category} kategorisi için {Count} adet piyasa kaydı başarıyla güncellendi ve önbellek tazelendi.",
                category, affectedCount);

            return affectedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "{Category} kategorisi piyasa verileri veritabanına kaydedilirken hata oluştu.", category);
            throw;
        }
    }
}
