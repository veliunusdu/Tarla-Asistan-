using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System.Reflection;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.UnitTests.Infrastructure;

public class DatabaseDefaultSentinelTests
{
    [Fact]
    public void EnumProperties_UseTheirDatabaseDefaultAsTheEfSentinel()
    {
        using var db = CreateContext();

        ShouldHaveSentinel(
            db.Model.FindEntityType(typeof(Activity))!.FindProperty(nameof(Activity.Status))!,
            ActivityStatus.Confirmed);
        ShouldHaveSentinel(
            db.Model.FindEntityType(typeof(FarmTask))!.FindProperty(nameof(FarmTask.Confidence))!,
            TaskConfidence.Medium);
        ShouldHaveSentinel(
            db.Model.FindEntityType(typeof(SupportCase))!.FindProperty(nameof(SupportCase.Priority))!,
            CasePriority.Medium);
    }

    private static void ShouldHaveSentinel(IProperty property, object expected)
    {
        var sentinel = property.GetType()
            .GetProperty("Sentinel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(property);

        sentinel.Should().Be(expected);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=tarla_test;Username=postgres;Password=postgres")
            .Options;
        return new ApplicationDbContext(options);
    }
}
