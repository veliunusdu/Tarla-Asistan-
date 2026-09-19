using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleAssignmentsAndActiveRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveRole",
                table: "refresh_tokens",
                type: "text",
                nullable: false,
                defaultValue: "Farmer");

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_GrantedByUserId",
                table: "user_role_assignments",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_RevokedByUserId",
                table: "user_role_assignments",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_UserId_Role",
                table: "user_role_assignments",
                columns: new[] { "UserId", "Role" },
                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");

            // Backfill: Tüm aktif kullanıcılar FARMER rolü alır (silinmiş/anonymized hesaplar hariç).
            migrationBuilder.Sql(
                @"INSERT INTO user_role_assignments (""Id"", ""UserId"", ""Role"", ""GrantedAtUtc"", ""GrantReason"")
                  SELECT gen_random_uuid(), ""Id"", 'Farmer', NOW(), 'Initial migration backfill'
                  FROM users
                  WHERE ""AccountStatus"" = 'Active' AND ""DeletedAtUtc"" IS NULL;");

            // Backfill: Eski Role=AGRONOMIST kullanıcılar ek olarak AGRONOMIST rolü alır.
            migrationBuilder.Sql(
                @"INSERT INTO user_role_assignments (""Id"", ""UserId"", ""Role"", ""GrantedAtUtc"", ""GrantReason"")
                  SELECT gen_random_uuid(), ""Id"", 'Agronomist', NOW(), 'Initial migration backfill for legacy Agronomist'
                  FROM users
                  WHERE ""AccountStatus"" = 'Active' AND ""DeletedAtUtc"" IS NULL AND ""Role"" = 'Agronomist';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_role_assignments");

            migrationBuilder.DropColumn(
                name: "ActiveRole",
                table: "refresh_tokens");
        }
    }
}
