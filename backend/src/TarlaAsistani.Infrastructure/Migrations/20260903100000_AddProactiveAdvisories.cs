using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TarlaAsistani.Infrastructure.Persistence;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903100000_AddProactiveAdvisories")]
public partial class AddProactiveAdvisories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "proactive_advisories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CropPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                AdvisoryType = table.Column<string>(type: "text", nullable: false),
                Severity = table.Column<string>(type: "text", nullable: false),
                ActionType = table.Column<string>(type: "text", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                AgronomicExplanation = table.Column<string>(type: "text", nullable: false),
                ActionRecommendation = table.Column<string>(type: "text", nullable: false),
                RecommendedDate = table.Column<DateOnly>(type: "date", nullable: true),
                MetricsJson = table.Column<string>(type: "text", nullable: true),
                DedupeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDismissed = table.Column<bool>(type: "boolean", nullable: false),
                DismissedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_proactive_advisories", x => x.Id);
                table.ForeignKey(
                    name: "FK_proactive_advisories_farms_FarmId",
                    column: x => x.FarmId,
                    principalTable: "farms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_proactive_advisories_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_proactive_advisories_crop_periods_CropPeriodId",
                    column: x => x.CropPeriodId,
                    principalTable: "crop_periods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_proactive_advisories_farm_tasks_RelatedTaskId",
                    column: x => x.RelatedTaskId,
                    principalTable: "farm_tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "ix_proactive_advisories_dedupe",
            table: "proactive_advisories",
            column: "DedupeKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_proactive_advisories_farm_active",
            table: "proactive_advisories",
            columns: new[] { "FarmId", "IsDismissed", "ValidUntilUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_proactive_advisories_user_active",
            table: "proactive_advisories",
            columns: new[] { "UserId", "IsDismissed" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "proactive_advisories");
    }
}
