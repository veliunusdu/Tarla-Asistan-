using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllFeatureEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CropPeriods_Farms_FarmId",
                table: "CropPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_Farms_Users_OwnerId",
                table: "Farms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Farms",
                table: "Farms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CropPeriods",
                table: "CropPeriods");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Farms",
                newName: "farms");

            migrationBuilder.RenameTable(
                name: "CropPeriods",
                newName: "crop_periods");

            migrationBuilder.RenameIndex(
                name: "IX_Users_PhoneNumber",
                table: "users",
                newName: "IX_users_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Farms_OwnerId_Name",
                table: "farms",
                newName: "ix_farms_owner_name");

            migrationBuilder.RenameIndex(
                name: "IX_Farms_OwnerId_ArchivedAt",
                table: "farms",
                newName: "ix_farms_owner_archived");

            migrationBuilder.RenameIndex(
                name: "IX_CropPeriods_FarmId_Status",
                table: "crop_periods",
                newName: "uq_crop_periods_one_active_per_farm");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "Farmer",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AccountStatus",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "AnonymizedSubjectId",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirebaseUid",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "SoilType",
                table: "farms",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "farms",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "farms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Variety",
                table: "crop_periods",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "crop_periods",
                type: "text",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "CropType",
                table: "crop_periods",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "crop_periods",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farms",
                table: "farms",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crop_periods",
                table: "crop_periods",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "account_deletion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirebaseUidSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NextRetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingOwnerToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FirebaseTokensRevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirestoreAnonymizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MediaDeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirebaseAuthDeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostgresAnonymizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_deletion_jobs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    CropPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Confirmed"),
                    Source = table.Column<string>(type: "text", nullable: false, defaultValue: "Manual"),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<float>(type: "real", nullable: true),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    VoiceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    VoiceTranscript = table.Column<string>(type: "text", nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Cost = table.Column<float>(type: "real", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activities_crop_periods_CropPeriodId",
                        column: x => x.CropPeriodId,
                        principalTable: "crop_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_activities_farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activities_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "client_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_operations_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "firebase_link_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirebaseUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_firebase_link_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_firebase_link_approvals_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_assets_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DeepLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    ProviderMessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Province = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    District = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "support_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false, defaultValue: "Medium"),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Open"),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_cases_farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_support_cases_users_AssignedExpertId",
                        column: x => x.AssignedExpertId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_support_cases_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    CropPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "New"),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<string>(type: "text", nullable: false, defaultValue: "Medium"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NotAppliedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ViewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tasks_crop_periods_CropPeriodId",
                        column: x => x.CropPeriodId,
                        principalTable: "crop_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "weather_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weather_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weather_snapshots_farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_revisions_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_revisions_users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "case_media",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_media", x => new { x.CaseId, x.MediaId });
                    table.ForeignKey(
                        name: "FK_case_media_media_assets_MediaId",
                        column: x => x.MediaId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_media_support_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "support_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false, defaultValue: "Comment"),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_messages_support_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "support_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pilot_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Open"),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    RelatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pilot_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pilot_feedback_support_cases_RelatedCaseId",
                        column: x => x.RelatedCaseId,
                        principalTable: "support_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pilot_feedback_tasks_RelatedTaskId",
                        column: x => x.RelatedTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pilot_feedback_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pilot_feedback_users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "case_message_media",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_message_media", x => new { x.MessageId, x.MediaId });
                    table.ForeignKey(
                        name: "FK_case_message_media_case_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "case_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_message_media_media_assets_MediaId",
                        column: x => x.MediaId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_AnonymizedSubjectId",
                table: "users",
                column: "AnonymizedSubjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_FirebaseUid",
                table: "users",
                column: "FirebaseUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_crop_periods_farm_planted",
                table: "crop_periods",
                columns: new[] { "FarmId", "PlantedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_jobs_UserId",
                table: "account_deletion_jobs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_jobs_retry_schedule",
                table: "account_deletion_jobs",
                columns: new[] { "Status", "NextRetryAtUtc", "LeaseUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_CropPeriodId",
                table: "activities",
                column: "CropPeriodId");

            migrationBuilder.CreateIndex(
                name: "ix_activities_farm_archived",
                table: "activities",
                columns: new[] { "FarmId", "ArchivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_farm_occurred",
                table: "activities",
                columns: new[] { "FarmId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "uq_activities_client_operation",
                table: "activities",
                columns: new[] { "CreatedById", "ClientOperationId" },
                unique: true,
                filter: "\"ClientOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_activities_task_id",
                table: "activities",
                column: "TaskId",
                unique: true,
                filter: "\"TaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_activity_revisions_ChangedById",
                table: "activity_revisions",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "ix_activity_revisions_activity_changed",
                table: "activity_revisions",
                columns: new[] { "ActivityId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_case_media_MediaId",
                table: "case_media",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_case_message_media_MediaId",
                table: "case_message_media",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_case_messages_SenderId",
                table: "case_messages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "ix_case_messages_case_created",
                table: "case_messages",
                columns: new[] { "CaseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_client_operations_actor_created",
                table: "client_operations",
                columns: new[] { "ActorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "uq_client_operations_actor_key",
                table: "client_operations",
                columns: new[] { "ActorId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_Token",
                table: "device_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_user_active",
                table: "device_tokens",
                columns: new[] { "UserId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_firebase_link_approvals_ExpiresAtUtc",
                table: "firebase_link_approvals",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "uq_firebase_link_approvals_one_unconsumed_per_uid",
                table: "firebase_link_approvals",
                column: "FirebaseUid",
                unique: true,
                filter: "\"ConsumedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_firebase_link_approvals_one_unconsumed_per_user",
                table: "firebase_link_approvals",
                column: "UserId",
                unique: true,
                filter: "\"ConsumedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_StorageKey",
                table: "media_assets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_created",
                table: "media_assets",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_DedupeKey",
                table: "notifications",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_status_created",
                table: "notifications",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_created",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_PhoneNumber",
                table: "otp_codes",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_pilot_feedback_CreatedById",
                table: "pilot_feedback",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_pilot_feedback_RelatedCaseId",
                table: "pilot_feedback",
                column: "RelatedCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_pilot_feedback_RelatedTaskId",
                table: "pilot_feedback",
                column: "RelatedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_pilot_feedback_ReviewedById",
                table: "pilot_feedback",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "ix_pilot_feedback_status_created",
                table: "pilot_feedback",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_pilot_feedback_type_created",
                table: "pilot_feedback",
                columns: new[] { "FeedbackType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_FamilyId",
                table: "refresh_tokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_support_cases_AssignedExpertId",
                table: "support_cases",
                column: "AssignedExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_support_cases_CreatedById",
                table: "support_cases",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "ix_support_cases_farm_created",
                table: "support_cases",
                columns: new[] { "FarmId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_support_cases_status_priority",
                table: "support_cases",
                columns: new[] { "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_CreatedById",
                table: "tasks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_CropPeriodId",
                table: "tasks",
                column: "CropPeriodId");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_farm_created",
                table: "tasks",
                columns: new[] { "FarmId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_farm_due_status",
                table: "tasks",
                columns: new[] { "FarmId", "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "uq_tasks_farm_due_dedupe",
                table: "tasks",
                columns: new[] { "FarmId", "DueDate", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weather_snapshots_farm_fetched",
                table: "weather_snapshots",
                columns: new[] { "FarmId", "FetchedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_crop_periods_farms_FarmId",
                table: "crop_periods",
                column: "FarmId",
                principalTable: "farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_farms_users_OwnerId",
                table: "farms",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_crop_periods_farms_FarmId",
                table: "crop_periods");

            migrationBuilder.DropForeignKey(
                name: "FK_farms_users_OwnerId",
                table: "farms");

            migrationBuilder.DropTable(
                name: "account_deletion_jobs");

            migrationBuilder.DropTable(
                name: "activity_revisions");

            migrationBuilder.DropTable(
                name: "case_media");

            migrationBuilder.DropTable(
                name: "case_message_media");

            migrationBuilder.DropTable(
                name: "client_operations");

            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.DropTable(
                name: "firebase_link_approvals");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "otp_codes");

            migrationBuilder.DropTable(
                name: "pilot_feedback");

            migrationBuilder.DropTable(
                name: "profiles");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "weather_snapshots");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "case_messages");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "support_cases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_AnonymizedSubjectId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_FirebaseUid",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farms",
                table: "farms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crop_periods",
                table: "crop_periods");

            migrationBuilder.DropIndex(
                name: "ix_crop_periods_farm_planted",
                table: "crop_periods");

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AnonymizedSubjectId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FirebaseUid",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "farms");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "crop_periods");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "farms",
                newName: "Farms");

            migrationBuilder.RenameTable(
                name: "crop_periods",
                newName: "CropPeriods");

            migrationBuilder.RenameIndex(
                name: "IX_users_PhoneNumber",
                table: "Users",
                newName: "IX_Users_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "ix_farms_owner_name",
                table: "Farms",
                newName: "IX_Farms_OwnerId_Name");

            migrationBuilder.RenameIndex(
                name: "ix_farms_owner_archived",
                table: "Farms",
                newName: "IX_Farms_OwnerId_ArchivedAt");

            migrationBuilder.RenameIndex(
                name: "uq_crop_periods_one_active_per_farm",
                table: "CropPeriods",
                newName: "IX_CropPeriods_FarmId_Status");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Farmer");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "SoilType",
                table: "Farms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "Farms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Variety",
                table: "CropPeriods",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "CropPeriods",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<int>(
                name: "CropType",
                table: "CropPeriods",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Farms",
                table: "Farms",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CropPeriods",
                table: "CropPeriods",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CropPeriods_Farms_FarmId",
                table: "CropPeriods",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Farms_Users_OwnerId",
                table: "Farms",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
