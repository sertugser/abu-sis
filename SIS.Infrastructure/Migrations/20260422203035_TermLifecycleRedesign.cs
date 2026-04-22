using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TermLifecycleRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AcademicTerms");

            migrationBuilder.AddColumn<bool>(
                name: "AddDropOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeEntryEnd",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "GradeEntryOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeEntryStart",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "RegistrationOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WithdrawalOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            // Data migration: eski 10 state enum → yeni 5 state enum
            // Draft(0) → Draft(0)
            // Planning(1), Scheduling(2) → Preparing(1)
            // Published(3), RegistrationOpen(4), AddDrop(5), WithdrawalPeriod(6), GradeEntry(7) → Ready(2)
            // Closed(8) → Closed(3)
            // Archived(9) → Archived(4)
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms"" SET ""Status"" = CASE
                    WHEN ""Status"" = 0 THEN 0
                    WHEN ""Status"" IN (1, 2) THEN 1
                    WHEN ""Status"" IN (3, 4, 5, 6, 7) THEN 2
                    WHEN ""Status"" = 8 THEN 3
                    WHEN ""Status"" = 9 THEN 4
                    ELSE 0
                END;");

            // Data migration: GradeEntryStart/End varsayılanları doldur
            // Varsayılan: GradeEntryStart = WithdrawalDeadline, GradeEntryEnd = TermEnd + 14 gün
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms""
                SET ""GradeEntryStart"" = ""WithdrawalDeadline"",
                    ""GradeEntryEnd"" = ""TermEnd"" + INTERVAL '14 days'
                WHERE ""GradeEntryStart"" = '0001-01-01T00:00:00Z'::timestamptz;");
        }

        /// <summary>
        /// UYARI: Down kısmı kayıplıdır.
        /// - Preparing (1) → Planning (1) olarak geri döner (Scheduling ayrımı kaybolur)
        /// - Ready (2) → Published (3) olarak geri döner (RegistrationOpen/AddDrop/WithdrawalPeriod/GradeEntry ayrımları kaybolur)
        /// - Override değerleri ve GradeEntry tarihleri tamamen silinir
        /// - IsActive kolonu geri eklenir ama Status == Ready olan satırlar true işaretlenir (en iyi tahmin)
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddDropOverride",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "GradeEntryEnd",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "GradeEntryOverride",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "GradeEntryStart",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "RegistrationOverride",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "WithdrawalOverride",
                table: "AcademicTerms");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AcademicTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Reverse status mapping (kayıplı — en iyi tahmin)
            // Ready (2) → Published (3), Preparing (1) → Planning (1)
            // Closed (3) → Closed (8), Archived (4) → Archived (9)
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms"" SET ""Status"" = CASE
                    WHEN ""Status"" = 0 THEN 0
                    WHEN ""Status"" = 1 THEN 1
                    WHEN ""Status"" = 2 THEN 3
                    WHEN ""Status"" = 3 THEN 8
                    WHEN ""Status"" = 4 THEN 9
                    ELSE 0
                END;
                UPDATE ""AcademicTerms"" SET ""IsActive"" = true WHERE ""Status"" = 3;");
        }
    }
}
