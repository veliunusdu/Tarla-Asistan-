using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Infrastructure.BackgroundServices;

public class ProactiveAdvisoryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProactiveAdvisoryBackgroundService> _logger;

    public ProactiveAdvisoryBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ProactiveAdvisoryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _configuration.GetValue("Advisories:ScanIntervalHours", 6);
        var interval = TimeSpan.FromHours(Math.Max(1, intervalHours));

        _logger.LogInformation("ProactiveAdvisoryBackgroundService started with scan interval {Hours} hours.", interval.TotalHours);

        // Initial brief delay on startup before first evaluation run
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunEvaluationScanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during proactive advisory evaluation scan.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunEvaluationScanAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var advisoryService = scope.ServiceProvider.GetRequiredService<IProactiveAdvisoryService>();

        var eligibleFarms = await db.Farms
            .AsNoTracking()
            .Where(f => f.ArchivedAt == null && f.Latitude != null && f.Longitude != null)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);

        _logger.LogInformation("Beginning proactive advisory scan for {Count} eligible farms.", eligibleFarms.Count);

        int evaluatedCount = 0;
        int errorCount = 0;

        foreach (var farm in eligibleFarms)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await advisoryService.EvaluateFarmAdvisoriesAsync(farm.Id, ct);
                evaluatedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to evaluate proactive advisories for farm {FarmName} ({FarmId}).", farm.Name, farm.Id);
            }

            await Task.Delay(300, ct);
        }

        sw.Stop();
        _logger.LogInformation(
            "Proactive advisory scan finished in {ElapsedMs}ms. Evaluated: {Evaluated}, Errors: {Errors}.",
            sw.ElapsedMilliseconds, evaluatedCount, errorCount);
    }
}
