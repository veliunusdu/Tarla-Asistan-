using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Market.Services;
using TarlaAsistani.Application.Features.Weather.Services;
using TarlaAsistani.Infrastructure.BackgroundJobs;
using TarlaAsistani.Infrastructure.BackgroundServices;
using TarlaAsistani.Infrastructure.Persistence;
using TarlaAsistani.Infrastructure.Services;
using TarlaAsistani.Infrastructure.Services.AI;
using TarlaAsistani.Infrastructure.Services.AI.DeepSeek;
using TarlaAsistani.Infrastructure.Services.AI.Gemini;

namespace TarlaAsistani.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. PostgreSQL + PostGIS Persistence
        var connectionString = NormalizeConnectionString(
            configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection")
        );

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

        // 4. Memory Cache & HTTP Integrations (Weather & AI)
        services.AddMemoryCache();
        var timeoutSeconds = configuration.GetValue("Weather:TimeoutSeconds", 10);
        services.AddHttpClient<OpenMeteoWeatherProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TarlaAsistani/1.0 (+https://tarla-asistani-api.onrender.com)");
        });
        services.AddHttpClient<WeatherApiWeatherProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TarlaAsistani/1.0 (+https://tarla-asistani-api.onrender.com)");
        });
        services.AddHttpClient<WttrInWeatherProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TarlaAsistani/1.0 (+https://tarla-asistani-api.onrender.com)");
        });
        services.AddScoped<IWeatherProvider>(sp =>
        {
            var openMeteo = sp.GetRequiredService<OpenMeteoWeatherProvider>();
            var weatherApi = sp.GetRequiredService<WeatherApiWeatherProvider>();
            var wttrIn = sp.GetRequiredService<WttrInWeatherProvider>();
            var logger = sp.GetRequiredService<ILogger<FallbackWeatherProvider>>();

            var providers = new List<IWeatherProvider>();
            if (weatherApi.IsConfigured)
            {
                providers.Add(weatherApi);
            }
            providers.Add(wttrIn);
            providers.Add(openMeteo);

            return new FallbackWeatherProvider(providers, logger);
        });
        var weatherActionRiskOptions = new WeatherActionRiskOptions();
        configuration.GetSection("Weather:ActionRisk").Bind(weatherActionRiskOptions);
        services.AddSingleton(weatherActionRiskOptions);
        services.AddSingleton<FarmWorkWeatherSignalEvaluator>();

        var aiProvider = FirstConfiguredValue(
            configuration["AI_CHAT_PROVIDER"],
            Environment.GetEnvironmentVariable("AI_CHAT_PROVIDER"),
            configuration["AI:Provider"])
            ?? "local";

        if (aiProvider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAIChatProvider, GeminiAIChatProvider>();
            services.AddHttpClient<IAIAgentProvider, GeminiAIAgentProvider>();
        }
        else if (aiProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAIChatProvider, DeepSeekAIChatProvider>();
            services.AddHttpClient<IAIAgentProvider, DeepSeekAIAgentProvider>();
        }
        else
        {
            services.AddScoped<IAIChatProvider, LocalAIChatProvider>();
            services.AddScoped<IAIAgentProvider, UnavailableAIAgentProvider>();
        }

        // 5. AI Services (Cost Calculator, Quota, Context & Proactive Advisories)
        services.AddSingleton<IAICostCalculator, AICostCalculator>();
        services.AddScoped<IAIQuotaService, AIQuotaService>();
        services.AddScoped<IAIContextService, AIContextService>();
        services.AddSingleton<IProactiveAdvisoryEngine, ProactiveAdvisoryEngine>();
        services.AddScoped<IProactiveAdvisoryService, ProactiveAdvisoryService>();

        // 6. Market Data Services & Providers
        services.Configure<StaticMarketDataOptions>(configuration.GetSection("Market:Static"));
        services.AddHttpClient<TcmbMarketDataProvider>();
        services.AddScoped<IMarketDataProvider, TcmbMarketDataProvider>();
        services.AddScoped<IMarketDataProvider, StaticMarketDataProvider>();
        services.AddScoped<MarketPriceSyncService>();

        // 7. Background Workers
        services.AddHostedService<AccountDeletionBackgroundService>();
        services.AddHostedService<ProactiveAdvisoryBackgroundService>();
        services.AddHostedService<MarketDataSyncWorker>();

        return services;
    }

    private static string? FirstConfiguredValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString) ||
            (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
             !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException("PostgreSQL URI must include username and password.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    private static void InitializeFirebase(IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance != null)
        {
            return;
        }

        var credentialsPath = configuration.GetValue<string>("Firebase:CredentialsPath");
        var projectId = configuration.GetValue<string>("Firebase:ProjectId");
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var firebaseAuthEnabled = bool.TryParse(
            FirstConfiguredValue(
                configuration["Firebase:AuthEnabled"],
                configuration["FIREBASE_AUTH_ENABLED"],
                Environment.GetEnvironmentVariable("FIREBASE_AUTH_ENABLED")),
            out var configuredFirebaseAuthEnabled)
            ? configuredFirebaseAuthEnabled
            : true;
        var requiresFirebaseAdmin = firebaseAuthEnabled &&
            (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(environmentName, "Staging", StringComparison.OrdinalIgnoreCase));

        if (requiresFirebaseAdmin && string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Firebase ProjectId must be configured in Production and Staging.");
        }

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

            if (requiresFirebaseAdmin && FirebaseApp.DefaultInstance == null)
            {
                throw new InvalidOperationException(
                    "Firebase Admin credentials must be configured in Production and Staging. " +
                    "Provide Firebase:CredentialsPath or GOOGLE_APPLICATION_CREDENTIALS.");
            }
        }
        catch (Exception ex)
        {
            if (requiresFirebaseAdmin)
            {
                throw new InvalidOperationException(
                    "Firebase Admin credentials could not be initialized in Production or Staging.",
                    ex);
            }
        }
    }
}
