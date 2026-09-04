using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.Infrastructure.BackgroundJobs;

/// <summary>
/// Piyasa verilerini (FX: 30 dakikada bir, Emtia: 8 saatte bir) periyodik olarak harici kaynaklardan
/// çekip veritabanına ve önbelleğe yansıtan arka plan servisi (IHostedService).
/// </summary>
public class MarketDataSyncWorker : BackgroundService
{
    private static readonly TimeSpan FxInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CommodityInterval = TimeSpan.FromHours(8);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketDataSyncWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter = new(2, 2);

    public MarketDataSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<MarketDataSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataSyncWorker başlatıldı. FX aralığı: {FxMinutes} dk, Emtia aralığı: {CommodityHours} saat.",
            FxInterval.TotalMinutes, CommodityInterval.TotalHours);

        // Başlangıçta tüm kategorileri hemen senkronize et (Cache warming)
        await SyncAllCategoriesAsync(stoppingToken);

        var nextFxSync = DateTime.UtcNow.Add(FxInterval);
        var nextCommoditySync = DateTime.UtcNow.Add(CommodityInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Her 1 dakikada bir zamanlama kontrolü yap
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                var now = DateTime.UtcNow;
                var tasks = new List<Task>();

                if (now >= nextFxSync)
                {
                    nextFxSync = now.Add(FxInterval);
                    tasks.Add(ExecuteCategorySyncWithLimitAsync(MarketCategory.Fx, stoppingToken));
                }

                if (now >= nextCommoditySync)
                {
                    nextCommoditySync = now.Add(CommodityInterval);
                    tasks.Add(ExecuteCategorySyncWithLimitAsync(MarketCategory.Fuel, stoppingToken));
                    tasks.Add(ExecuteCategorySyncWithLimitAsync(MarketCategory.Fertilizer, stoppingToken));
                    tasks.Add(ExecuteCategorySyncWithLimitAsync(MarketCategory.Crop, stoppingToken));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketDataSyncWorker döngüsünde beklenmeyen bir hata oluştu.");
            }
        }

        _logger.LogInformation("MarketDataSyncWorker normal bir şekilde durduruldu.");
    }

    private async Task SyncAllCategoriesAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Uygulama başlangıcı: Tüm piyasa kategorileri için önbellek ısıtma (cache warming) başlatılıyor...");
        var sw = Stopwatch.StartNew();

        var categories = new[]
        {
            MarketCategory.Fx,
            MarketCategory.Fuel,
            MarketCategory.Fertilizer,
            MarketCategory.Crop
        };

        var tasks = categories.Select(cat => ExecuteCategorySyncWithLimitAsync(cat, stoppingToken));
        await Task.WhenAll(tasks);

        sw.Stop();
        _logger.LogInformation("Başlangıç piyasa verisi senkronizasyonu tamamlandı. Geçen süre: {ElapsedMs} ms.", sw.ElapsedMilliseconds);
    }

    private async Task ExecuteCategorySyncWithLimitAsync(MarketCategory category, CancellationToken stoppingToken)
    {
        await _concurrencyLimiter.WaitAsync(stoppingToken);
        var sw = Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<MarketPriceSyncService>();

            var updatedCount = await syncService.SyncAsync(category, stoppingToken);
            sw.Stop();

            _logger.LogInformation("{Category} kategorisi senkronizasyonu tamamlandı. Güncellenen: {Count} kayıt, Süre: {ElapsedMs} ms.",
                category, updatedCount, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapatma akışı
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "{Category} kategorisi senkronizasyonu sırasında hata oluştu. Süre: {ElapsedMs} ms.",
                category, sw.ElapsedMilliseconds);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
