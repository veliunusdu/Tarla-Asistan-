using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TarlaAsistani.Infrastructure.Persistence;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902224500_LockDownMigrationHistory")]
public partial class LockDownMigrationHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
            REVOKE ALL PRIVILEGES ON TABLE public."__EFMigrationsHistory" FROM anon, authenticated;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollbacks must not expose schema metadata through the Supabase Data API.
    }
}
