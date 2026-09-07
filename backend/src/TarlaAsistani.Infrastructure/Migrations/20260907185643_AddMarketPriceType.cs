using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketPriceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "market_prices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "reference");

            migrationBuilder.Sql("UPDATE market_prices SET \"PriceType\" = CASE WHEN \"Source\" = 'TCMB' THEN 'live' ELSE 'reference' END");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111105"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111106"),
                column: "PriceType",
                value: "reference");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111107"),
                column: "PriceType",
                value: "live");

            migrationBuilder.UpdateData(
                table: "market_prices",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111108"),
                column: "PriceType",
                value: "live");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "market_prices");
        }
    }
}
