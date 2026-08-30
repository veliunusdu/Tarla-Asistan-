using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_UsesDatabaseUrlWhenDefaultConnectionIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgresql://tarla:secret@db.example.com:5432/tarla"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var connectionString = db.Database.GetConnectionString();
        Assert.Contains("Host=db.example.com", connectionString);
        Assert.Contains("Database=tarla", connectionString);
    }

    [Fact]
    public void AddInfrastructure_PrefersDatabaseUrlOverLocalDefaultConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=local;Username=postgres;Password=postgres",
                ["DATABASE_URL"] = "postgresql://tarla:secret@cloud.example.com:5432/production"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var connectionString = db.Database.GetConnectionString();
        Assert.Contains("Host=cloud.example.com", connectionString);
        Assert.Contains("Database=production", connectionString);
    }
}
