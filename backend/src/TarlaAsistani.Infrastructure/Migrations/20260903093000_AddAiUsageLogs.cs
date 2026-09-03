using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TarlaAsistani.Infrastructure.Persistence;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903093000_AddAiUsageLogs")]
public partial class AddAiUsageLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ai_usage_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                HasPhoto = table.Column<bool>(type: "boolean", nullable: false),
                PromptTokens = table.Column<int>(type: "integer", nullable: false),
                CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                TotalTokens = table.Column<int>(type: "integer", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                DurationMs = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_usage_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ai_usage_logs_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_ai_usage_logs_created",
            table: "ai_usage_logs",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "ix_ai_usage_logs_user_created",
            table: "ai_usage_logs",
            columns: new[] { "UserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ai_usage_logs");
    }
}
