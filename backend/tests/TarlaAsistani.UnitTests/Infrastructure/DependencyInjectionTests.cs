using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Infrastructure;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WhenProductionFirebaseConfigurationIsMissing_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["FIREBASE_AUTH_ENABLED"] = "true",
                ["Firebase:ProjectId"] = "demo2-c4265",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=tarla;Username=postgres;Password=postgres"
            })
            .Build();
        var services = new ServiceCollection();

        var action = () => services.AddInfrastructure(config);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Firebase Admin credentials*Production*");
    }

    [Fact]
    public void AddInfrastructure_WhenEnvironmentProviderOverridesLocalDefault_RegistersDeepSeekProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=tarla;Username=postgres;Password=postgres",
                ["AI:Provider"] = "local",
                ["AI_CHAT_PROVIDER"] = "deepseek"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddInfrastructure(config);

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IAIChatProvider>()
            .Should().BeOfType<DeepSeekAIChatProvider>();
    }

    [Fact]
    public void AddInfrastructure_RegistersSevenDayWeatherProviderBeforeShortRangeFallbacks()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=tarla;Username=postgres;Password=postgres"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddInfrastructure(config);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var fallback = scope.ServiceProvider.GetRequiredService<IWeatherProvider>()
            .Should().BeOfType<FallbackWeatherProvider>().Subject;
        var providersField = typeof(FallbackWeatherProvider).GetField(
            "_providers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var providers = providersField!.GetValue(fallback)
            .Should().BeAssignableTo<IReadOnlyList<IWeatherProvider>>().Subject;

        providers.Select(provider => provider.Name).Should().ContainInOrder(
            "open_meteo",
            "wttr_in");
        providers[0].Name.Should().Be("open_meteo");
    }
}
