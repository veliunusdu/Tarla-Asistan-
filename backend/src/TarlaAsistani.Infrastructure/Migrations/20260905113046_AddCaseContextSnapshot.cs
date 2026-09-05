using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseContextSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_context_snapshots",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    SizeInHectares = table.Column<double>(type: "double precision", nullable: true),
                    IrrigationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SoilType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FarmNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CropName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CropPlantedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CropHarvestedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CropGrowingDay = table.Column<int>(type: "integer", nullable: true),
                    WeatherProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WeatherFetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsBasedOnStaleWeather = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentTemperatureC = table.Column<double>(type: "double precision", nullable: true),
                    CurrentHumidityPercent = table.Column<double>(type: "double precision", nullable: true),
                    Next24HoursPrecipitationMm = table.Column<double>(type: "double precision", nullable: true),
                    RecentActivitiesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_context_snapshots", x => x.CaseId);
                    table.ForeignKey(
                        name: "FK_case_context_snapshots_support_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "support_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_context_snapshots");
        }
    }
}
