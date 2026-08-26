using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Infrastructure.BackgroundServices;
using TarlaAsistani.Infrastructure.Persistence;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
            });
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IMediaStorageService, FileMediaStorageService>();
        services.AddScoped<IPushNotificationService, MockPushNotificationService>();

        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>();
        services.AddHttpClient<IAIChatProvider, DeepSeekAIChatProvider>();

        services.AddHostedService<AccountDeletionBackgroundService>();

        return services;
    }
}