using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Services;

namespace TarlaAsistani.UnitTests.Infrastructure;

public class DependencyInjectionTests
{
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
}
