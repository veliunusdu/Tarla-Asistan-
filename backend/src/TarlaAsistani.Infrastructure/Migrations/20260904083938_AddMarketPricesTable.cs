using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketPricesTable : Migration
    {
        /// <inheritdoc />
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

            migrationBuilder.CreateTable(
                name: "market_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ChangePercent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_prices", x => x.Id);
                });

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
                        name: "FK_proactive_advisories_crop_periods_CropPeriodId",
                        column: x => x.CropPeriodId,
                        principalTable: "crop_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_proactive_advisories_farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_proactive_advisories_tasks_RelatedTaskId",
                        column: x => x.RelatedTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_proactive_advisories_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "market_prices",
                columns: new[] { "Id", "Category", "ChangePercent", "Code", "CurrentPrice", "Name", "PreviousPrice", "Source", "Unit", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "Fuel", 1.47m, "DIESEL", 44.85m, "Motorin (Mazot)", 44.20m, "EPDK", "TL/Lt", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "Fuel", 0.00m, "GASOLINE", 43.20m, "Benzin (95 Oktan)", 43.20m, "EPDK", "TL/Lt", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "Fertilizer", -1.72m, "UREA", 14250.00m, "Üre Gübresi (%46 N)", 14500.00m, "GUBRETAS", "TL/Ton", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111104"), "Fertilizer", 0.00m, "DAP", 20800.00m, "DAP Gübresi (18-46-0)", 20800.00m, "GUBRETAS", "TL/Ton", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111105"), "Crop", 1.55m, "WHEAT", 9850.00m, "Ekmeklik Buğday", 9700.00m, "TURIB", "TL/Ton", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111106"), "Crop", -0.61m, "CORN", 8200.00m, "Mısır (1. Sınıf)", 8250.00m, "TURIB", "TL/Ton", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111107"), "Fx", 0.12m, "USD_TRY", 34.2200m, "Dolar", 34.1800m, "TCMB", "TL", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-1111-1111-1111-111111111108"), "Fx", -0.18m, "EUR_TRY", 37.9500m, "Euro", 38.0200m, "TCMB", "TL", new DateTime(2024, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_created",
                table: "ai_usage_logs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_user_created",
                table: "ai_usage_logs",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_market_prices_Code",
                table: "market_prices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_proactive_advisories_CropPeriodId",
                table: "proactive_advisories",
                column: "CropPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_proactive_advisories_RelatedTaskId",
                table: "proactive_advisories",
                column: "RelatedTaskId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "market_prices");

            migrationBuilder.DropTable(
                name: "proactive_advisories");
        }
    }
}
