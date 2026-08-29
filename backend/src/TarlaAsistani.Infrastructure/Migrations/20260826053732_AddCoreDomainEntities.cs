using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmerPhoneNumber",
                table: "Farms");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Farms",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Farms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IrrigationMethod",
                table: "Farms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Farms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Farms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Farms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Farms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<double>(
                name: "SizeInHectares",
                table: "Farms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoilType",
                table: "Farms",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CropPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    CropType = table.Column<int>(type: "integer", nullable: false),
                    Variety = table.Column<string>(type: "text", nullable: true),
                    PlantedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    HarvestedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropPeriods_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Farms_OwnerId_ArchivedAt",
                table: "Farms",
                columns: new[] { "OwnerId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Farms_OwnerId_Name",
                table: "Farms",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CropPeriods_FarmId_Status",
                table: "CropPeriods",
                columns: new[] { "FarmId", "Status" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Farms_Users_OwnerId",
                table: "Farms",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Farms_Users_OwnerId",
                table: "Farms");

            migrationBuilder.DropTable(
                name: "CropPeriods");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Farms_OwnerId_ArchivedAt",
                table: "Farms");

            migrationBuilder.DropIndex(
                name: "IX_Farms_OwnerId_Name",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "IrrigationMethod",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "SizeInHectares",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "SoilType",
                table: "Farms");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Farms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<string>(
                name: "FarmerPhoneNumber",
                table: "Farms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }
    }
}
