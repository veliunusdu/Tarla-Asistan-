using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
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
        // 1. PostgreSQL + PostGIS Persistence
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
            });
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // 2. Firebase Initialization (FCM & Auth)
        InitializeFirebase(configuration);

        services.AddScoped<IFirebaseAuthService, FirebaseAuthService>();
        services.AddScoped<IPushNotificationService, FirebasePushNotificationService>();

        // 3. Security & Media Services
        services.AddScoped<IJwtService, JwtService>();

        var mediaProvider = configuration["Media:Provider"]
            ?? configuration["MEDIA_STORAGE_PROVIDER"]
            ?? Environment.GetEnvironmentVariable("MEDIA_STORAGE_PROVIDER")
            ?? "local";

        if (mediaProvider.Equals("r2", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IMediaStorageService, R2MediaStorageService>();
        }
        else
        {
            services.AddScoped<IMediaStorageService, FileMediaStorageService>();
        }

        // 4. HTTP Integrations (Weather & AI)
        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>();

        var aiProvider = configuration["AI:Provider"]
            ?? configuration["AI_CHAT_PROVIDER"]
            ?? Environment.GetEnvironmentVariable("AI_CHAT_PROVIDER")
            ?? "local";

        if (aiProvider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAIChatProvider, GeminiAIChatProvider>();
        }
        else if (aiProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAIChatProvider, DeepSeekAIChatProvider>();
        }
        else
        {
            services.AddScoped<IAIChatProvider, LocalAIChatProvider>();
        }

        // 5. Background Workers
        services.AddHostedService<AccountDeletionBackgroundService>();

        return services;
    }

    private static void InitializeFirebase(IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance != null)
        {
            return;
        }

        var credentialsPath = configuration.GetValue<string>("Firebase:CredentialsPath");
        var projectId = configuration.GetValue<string>("Firebase:ProjectId");

        try
        {
            string? foundPath = null;
            if (!string.IsNullOrWhiteSpace(credentialsPath))
            {
                var candidatePaths = new[]
                {
                    credentialsPath,
                    Path.Combine(Directory.GetCurrentDirectory(), credentialsPath),
                    Path.Combine(AppContext.BaseDirectory, credentialsPath),
                    Path.Combine("..", credentialsPath),
                    Path.Combine("..", "..", credentialsPath),
                    Path.Combine("..", "backend", credentialsPath),
                    Path.Combine("backend", credentialsPath),
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", credentialsPath))
                };

                foundPath = candidatePaths.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
            }

            if (foundPath != null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(foundPath),
                    ProjectId = projectId
                });
            }
            else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                    ProjectId = projectId
                });
            }
        }
        catch
        {
            // Silently fall back to development / uninitialized mode
        }
    }
}
