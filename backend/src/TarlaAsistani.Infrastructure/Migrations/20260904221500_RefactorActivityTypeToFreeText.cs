using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TarlaAsistani.Infrastructure.Persistence;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260904221500_RefactorActivityTypeToFreeText")]
public partial class RefactorActivityTypeToFreeText : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add ActivityName column as nullable initially
        migrationBuilder.AddColumn<string>(
            name: "ActivityName",
            table: "activities",
            type: "character varying(150)",
            maxLength: 150,
            nullable: true);

        // 2. Data migration for existing records:
        //    - If linked to a task and title exists, use task Title
        //    - Otherwise map known enum names to Turkish titles
        //    - Preserve custom note/description if type was Other
        migrationBuilder.Sql(@"
            -- 2a. Activities linked to tasks
            UPDATE activities a
            SET ""ActivityName"" = SUBSTRING(TRIM(t.""Title""), 1, 150)
            FROM tasks t
            WHERE a.""TaskId"" = t.""Id""
              AND t.""Title"" IS NOT NULL
              AND TRIM(t.""Title"") <> '';

            -- 2b. Map standard enum names to farmer-friendly Turkish text
            UPDATE activities
            SET ""ActivityName"" = CASE
                WHEN ""ActivityType"" = 'Irrigation' THEN 'Sulama'
                WHEN ""ActivityType"" = 'Fertilization' THEN 'Gübreleme'
                WHEN ""ActivityType"" = 'Spraying' THEN 'İlaçlama'
                WHEN ""ActivityType"" = 'Pruning' THEN 'Budama'
                WHEN ""ActivityType"" = 'FieldCheck' THEN 'Tarla Kontrolü'
                WHEN ""ActivityType"" = 'Harvest' THEN 'Hasat'
                WHEN ""ActivityType"" = 'Other' AND ""Description"" IS NOT NULL AND TRIM(""Description"") <> '' AND LENGTH(TRIM(""Description"")) <= 150 THEN TRIM(""Description"")
                WHEN ""ActivityType"" = 'Other' THEN 'Diğer'
                ELSE COALESCE(""ActivityType"", 'Faaliyet')
            END
            WHERE ""ActivityName"" IS NULL OR TRIM(""ActivityName"") = '';

            -- 2c. Fallback for any remaining unmapped records
            UPDATE activities
            SET ""ActivityName"" = 'Faaliyet'
            WHERE ""ActivityName"" IS NULL OR TRIM(""ActivityName"") = '';
        ");

        // 3. Enforce NOT NULL on ActivityName
        migrationBuilder.AlterColumn<string>(
            name: "ActivityName",
            table: "activities",
            type: "character varying(150)",
            maxLength: 150,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(150)",
            oldMaxLength: 150,
            oldNullable: true);

        // 4. Make ActivityType nullable
        migrationBuilder.AlterColumn<string>(
            name: "ActivityType",
            table: "activities",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE activities
            SET ""ActivityType"" = CASE
                WHEN LOWER(TRIM(""ActivityName"")) = 'sulama' THEN 'Irrigation'
                WHEN LOWER(TRIM(""ActivityName"")) = 'gübreleme' THEN 'Fertilization'
                WHEN LOWER(TRIM(""ActivityName"")) = 'ilaçlama' THEN 'Spraying'
                WHEN LOWER(TRIM(""ActivityName"")) = 'budama' THEN 'Pruning'
                WHEN LOWER(TRIM(""ActivityName"")) = 'tarla kontrolü' THEN 'FieldCheck'
                WHEN LOWER(TRIM(""ActivityName"")) = 'hasat' THEN 'Harvest'
                ELSE 'Other'
            END
            WHERE ""ActivityType"" IS NULL;
        ");

        migrationBuilder.AlterColumn<string>(
            name: "ActivityType",
            table: "activities",
            type: "text",
            nullable: false,
            defaultValue: "Other",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "ActivityName",
            table: "activities");
    }
}
