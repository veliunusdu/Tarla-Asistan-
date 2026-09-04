using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TarlaAsistani.Infrastructure.Persistence;

#nullable disable

namespace TarlaAsistani.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260904223500_RefactorCropTypeToFreeText")]
public partial class RefactorCropTypeToFreeText : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add CropName column as nullable initially
        migrationBuilder.AddColumn<string>(
            name: "CropName",
            table: "crop_periods",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        // 2. Data migration for existing records:
        //    Map standard enum names to farmer-friendly Turkish text
        migrationBuilder.Sql(@"
            UPDATE crop_periods
            SET ""CropName"" = CASE
                WHEN ""CropType"" = 'Wheat' THEN 'Buğday'
                WHEN ""CropType"" = 'Barley' THEN 'Arpa'
                WHEN ""CropType"" = 'Corn' THEN 'Mısır'
                WHEN ""CropType"" = 'Sunflower' THEN 'Ayçiçeği'
                WHEN ""CropType"" = 'Tomato' THEN 'Domates'
                ELSE COALESCE(""CropType"", 'Ürün')
            END
            WHERE ""CropName"" IS NULL OR TRIM(""CropName"") = '';

            -- Fallback for any remaining null/empty
            UPDATE crop_periods
            SET ""CropName"" = 'Ürün'
            WHERE ""CropName"" IS NULL OR TRIM(""CropName"") = '';
        ");

        // 3. Enforce NOT NULL on CropName
        migrationBuilder.AlterColumn<string>(
            name: "CropName",
            table: "crop_periods",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        // 4. Make CropType nullable
        migrationBuilder.AlterColumn<string>(
            name: "CropType",
            table: "crop_periods",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE crop_periods
            SET ""CropType"" = CASE LOWER(TRIM(""CropName""))
                WHEN 'buğday' THEN 'Wheat'
                WHEN 'arpa' THEN 'Barley'
                WHEN 'mısır' THEN 'Corn'
                WHEN 'ayçiçeği' THEN 'Sunflower'
                WHEN 'domates' THEN 'Tomato'
                ELSE NULL
            END
            WHERE ""CropType"" IS NULL;

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM crop_periods WHERE ""CropType"" IS NULL) THEN
                    RAISE EXCEPTION 'Cannot roll back CropName while non-canonical crop names exist. Restore a backup or migrate those records first.';
                END IF;
            END $$;
        ");

        migrationBuilder.AlterColumn<string>(
            name: "CropType",
            table: "crop_periods",
            type: "text",
            nullable: false,
            defaultValue: "Wheat",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "CropName",
            table: "crop_periods");
    }
}
