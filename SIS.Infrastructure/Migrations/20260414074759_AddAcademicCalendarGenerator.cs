using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCalendarGenerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TermCalendarEvents_TermId",
                table: "TermCalendarEvents");

            migrationBuilder.AddColumn<bool>(
                name: "BlocksAcademicEvents",
                table: "TermCalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChainCode",
                table: "TermCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeneratedDraftId",
                table: "TermCalendarEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGenerated",
                table: "TermCalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateItemKey",
                table: "TermCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AcademicTerms"
                SET "RegistrationStart" = TIMESTAMPTZ '2025-09-15T00:00:00Z',
                    "RegistrationEnd" = TIMESTAMPTZ '2025-09-19T00:00:00Z',
                    "AddDropStart" = TIMESTAMPTZ '2025-09-29T00:00:00Z',
                    "AddDropEnd" = TIMESTAMPTZ '2025-10-03T00:00:00Z',
                    "WithdrawalDeadline" = TIMESTAMPTZ '2025-11-07T00:00:00Z',
                    "TermStart" = TIMESTAMPTZ '2025-09-22T00:00:00Z',
                    "TermEnd" = TIMESTAMPTZ '2026-01-02T00:00:00Z',
                    "UpdatedAt" = NOW()
                WHERE "Year" = 2025 AND "Semester" = 1 AND "Status" < 8;

                UPDATE "AcademicTerms"
                SET "RegistrationStart" = TIMESTAMPTZ '2026-02-16T00:00:00Z',
                    "RegistrationEnd" = TIMESTAMPTZ '2026-02-20T00:00:00Z',
                    "AddDropStart" = TIMESTAMPTZ '2026-03-02T00:00:00Z',
                    "AddDropEnd" = TIMESTAMPTZ '2026-03-06T00:00:00Z',
                    "WithdrawalDeadline" = TIMESTAMPTZ '2026-04-03T00:00:00Z',
                    "TermStart" = TIMESTAMPTZ '2026-02-23T00:00:00Z',
                    "TermEnd" = TIMESTAMPTZ '2026-06-05T00:00:00Z',
                    "UpdatedAt" = NOW()
                WHERE "Year" = 2025 AND "Semester" = 2 AND "Status" < 8;

                INSERT INTO "AcademicTerms" ("Name", "ShortName", "EnglishName", "Year", "Semester", "TermType",
                    "RegistrationStart", "RegistrationEnd", "AddDropStart", "AddDropEnd", "WithdrawalDeadline",
                    "TermStart", "TermEnd", "IsActive", "Status", "CreatedAt", "UpdatedAt")
                SELECT '2025-2026 Yaz', '25-26 Yaz', 'Summer 2025-2026', 2025, 3, 2,
                    TIMESTAMPTZ '2026-06-30T00:00:00Z', TIMESTAMPTZ '2026-07-03T00:00:00Z',
                    TIMESTAMPTZ '2026-06-30T00:00:00Z', TIMESTAMPTZ '2026-07-03T00:00:00Z',
                    TIMESTAMPTZ '2026-08-28T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z',
                    TIMESTAMPTZ '2026-08-28T00:00:00Z', FALSE, 0, NOW(), NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "AcademicTerms" WHERE "Year" = 2025 AND "Semester" = 3
                );

                UPDATE "TermCalendarEvents"
                SET "BlocksAcademicEvents" = TRUE
                WHERE "EventType" IN (17, 18, 49);
                """);

            migrationBuilder.CreateTable(
                name: "CalendarConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConstraintType = table.Column<int>(type: "integer", nullable: false),
                    TargetEventType = table.Column<int>(type: "integer", nullable: true),
                    TargetChainCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ParameterJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsHard = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarConstraints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ChainCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedCalendarDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EventsJson = table.Column<string>(type: "jsonb", nullable: false),
                    HolidaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedCalendarDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FixedMonth = table.Column<int>(type: "integer", nullable: true),
                    FixedDay = table.Column<int>(type: "integer", nullable: true),
                    HijriMonth = table.Column<int>(type: "integer", nullable: true),
                    HijriDay = table.Column<int>(type: "integer", nullable: true),
                    DurationDays = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    IsHalfDayStart = table.Column<bool>(type: "boolean", nullable: false),
                    IsOfficialHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksAcademicEvents = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OverridesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermCalendarEvents_IsGenerated_GeneratedDraftId",
                table: "TermCalendarEvents",
                columns: new[] { "IsGenerated", "GeneratedDraftId" });

            migrationBuilder.CreateIndex(
                name: "IX_TermCalendarEvents_TermId_StartDate",
                table: "TermCalendarEvents",
                columns: new[] { "TermId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConstraints_AcademicYear_ConstraintType",
                table: "CalendarConstraints",
                columns: new[] { "AcademicYear", "ConstraintType" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarTemplates_ChainCode",
                table: "CalendarTemplates",
                column: "ChainCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCalendarDrafts_AcademicYear_Status",
                table: "GeneratedCalendarDrafts",
                columns: new[] { "AcademicYear", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCalendarDrafts_ExpiresAt",
                table: "GeneratedCalendarDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayDefinitions_Name",
                table: "HolidayDefinitions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarConstraints");

            migrationBuilder.DropTable(
                name: "CalendarTemplates");

            migrationBuilder.DropTable(
                name: "GeneratedCalendarDrafts");

            migrationBuilder.DropTable(
                name: "HolidayDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TermCalendarEvents_IsGenerated_GeneratedDraftId",
                table: "TermCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_TermCalendarEvents_TermId_StartDate",
                table: "TermCalendarEvents");

            migrationBuilder.DropColumn(
                name: "BlocksAcademicEvents",
                table: "TermCalendarEvents");

            migrationBuilder.DropColumn(
                name: "ChainCode",
                table: "TermCalendarEvents");

            migrationBuilder.DropColumn(
                name: "GeneratedDraftId",
                table: "TermCalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsGenerated",
                table: "TermCalendarEvents");

            migrationBuilder.DropColumn(
                name: "TemplateItemKey",
                table: "TermCalendarEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TermCalendarEvents_TermId",
                table: "TermCalendarEvents",
                column: "TermId");
        }
    }
}
