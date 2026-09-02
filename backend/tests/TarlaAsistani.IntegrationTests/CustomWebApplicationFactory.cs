using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "IntegrationTestDb_" + Guid.NewGuid().ToString("N");
    private readonly string _environmentName;

    public CustomWebApplicationFactory() : this("Testing")
    {
    }

    internal CustomWebApplicationFactory(string environmentName)
    {
        _environmentName = environmentName;
    }

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseUpper) }
    };

    public Mock<IWeatherProvider> MockWeatherProvider { get; } = new();
    public Mock<IAIChatProvider> MockAIChatProvider { get; } = new();
    public Mock<IFirebaseAuthService> MockFirebaseAuthService { get; } = new();
    public Mock<IPushNotificationService> MockPushService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.UseSetting("Auth:JwtSecret", "integration-test-jwt-secret-change-me-32-chars!");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        // Firebase-backed authentication is replaced by a mock for integration tests.
        builder.UseSetting("FIREBASE_AUTH_ENABLED", "false");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });

        builder.ConfigureServices(services =>
        {
            // 1. Remove existing ApplicationDbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // 2. Add In-Memory Database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // 3. Replace External HTTP Services with Mocks
            var weatherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWeatherProvider));
            if (weatherDescriptor != null) services.Remove(weatherDescriptor);
            services.AddSingleton(MockWeatherProvider.Object);

            var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIChatProvider));
            if (aiDescriptor != null) services.Remove(aiDescriptor);
            services.AddSingleton(MockAIChatProvider.Object);

            var firebaseAuthDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFirebaseAuthService));
            if (firebaseAuthDescriptor != null) services.Remove(firebaseAuthDescriptor);
            services.AddSingleton(MockFirebaseAuthService.Object);

            var pushDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPushNotificationService));
            if (pushDescriptor != null) services.Remove(pushDescriptor);
            services.AddSingleton(MockPushService.Object);
        });

        builder.UseEnvironment(_environmentName);
    }
}
