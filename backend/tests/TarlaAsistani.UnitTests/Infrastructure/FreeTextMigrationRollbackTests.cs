using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TarlaAsistani.Infrastructure.Migrations;

namespace TarlaAsistani.UnitTests.Infrastructure;

public class FreeTextMigrationRollbackTests
{
    [Fact]
    public void ActivityRollback_NormalizesNullableLegacyTypeBeforeRequiringIt()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new TestActivityMigration().BuildDown(builder);

        builder.Operations.OfType<SqlOperation>().Should().ContainSingle(operation =>
            operation.Sql.Contains("UPDATE activities", StringComparison.Ordinal) &&
            operation.Sql.Contains("\"ActivityType\" IS NULL", StringComparison.Ordinal));
    }

    [Fact]
    public void CropRollback_NormalizesNullableLegacyTypeBeforeRequiringIt()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new TestCropMigration().BuildDown(builder);

        builder.Operations.OfType<SqlOperation>().Should().ContainSingle(operation =>
            operation.Sql.Contains("UPDATE crop_periods", StringComparison.Ordinal) &&
            operation.Sql.Contains("\"CropType\" IS NULL", StringComparison.Ordinal));
    }

    private sealed class TestActivityMigration : RefactorActivityTypeToFreeText
    {
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }

    private sealed class TestCropMigration : RefactorCropTypeToFreeText
    {
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}
