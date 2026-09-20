using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleAndExpertWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaseId",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "firebase_link_approvals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agronomist_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    ApplicantNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agronomist_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agronomist_applications_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agronomist_applications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_assignment_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_assignment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_assignment_events_support_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "support_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_assignment_events_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_assignment_events_users_NewExpertId",
                        column: x => x.NewExpertId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_assignment_events_users_PreviousExpertId",
                        column: x => x.PreviousExpertId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_case_id",
                table: "tasks",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_firebase_link_approvals_ApprovedByUserId",
                table: "firebase_link_approvals",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_agronomist_applications_ReviewedByUserId",
                table: "agronomist_applications",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_agronomist_applications_status",
                table: "agronomist_applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "uq_agronomist_applications_one_pending_per_user",
                table: "agronomist_applications",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_case_assignment_events_case_changed",
                table: "case_assignment_events",
                columns: new[] { "CaseId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_events_ChangedByUserId",
                table: "case_assignment_events",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_events_NewExpertId",
                table: "case_assignment_events",
                column: "NewExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_events_PreviousExpertId",
                table: "case_assignment_events",
                column: "PreviousExpertId");

            migrationBuilder.AddForeignKey(
                name: "FK_firebase_link_approvals_users_ApprovedByUserId",
                table: "firebase_link_approvals",
                column: "ApprovedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_support_cases_CaseId",
                table: "tasks",
                column: "CaseId",
                principalTable: "support_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_firebase_link_approvals_users_ApprovedByUserId",
                table: "firebase_link_approvals");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_support_cases_CaseId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "agronomist_applications");

            migrationBuilder.DropTable(
                name: "case_assignment_events");

            migrationBuilder.DropIndex(
                name: "ix_tasks_case_id",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_firebase_link_approvals_ApprovedByUserId",
                table: "firebase_link_approvals");

            migrationBuilder.DropColumn(
                name: "CaseId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "firebase_link_approvals");
        }
    }
}
