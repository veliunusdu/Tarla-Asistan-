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
                name: "IX_market_prices_Code",
                table: "market_prices",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_prices");
        }
    }
}
