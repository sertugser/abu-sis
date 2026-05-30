using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTermAttendancePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermAttendancePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<int>(type: "integer", nullable: true),
                    TermWeekCount = table.Column<int>(type: "integer", nullable: false),
                    TheoryAbsenceLimitPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    LabAbsenceLimitPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermAttendancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermAttendancePolicies_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TermAttendancePolicies_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_ProgramId",
                table: "TermAttendancePolicies",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_TermId",
                table: "TermAttendancePolicies",
                column: "TermId",
                unique: true,
                filter: "\"ProgramId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_TermId_ProgramId",
                table: "TermAttendancePolicies",
                columns: new[] { "TermId", "ProgramId" },
                unique: true,
                filter: "\"ProgramId\" IS NOT NULL");

            // Backfill: mevcut her döneme, o anki global SystemConfig değerlerinden bir
            // "dönem geneli" (ProgramId = NULL) politika satırı yaz. Böylece geçmiş dönemler
            // kendi limitlerini taşır (dondurulur); global sonradan değişse de etkilenmezler.
            migrationBuilder.Sql(@"
                INSERT INTO ""TermAttendancePolicies""
                    (""TermId"", ""ProgramId"", ""TermWeekCount"",
                     ""TheoryAbsenceLimitPercent"", ""LabAbsenceLimitPercent"", ""Method"", ""CreatedAt"")
                SELECT
                    t.""Id"",
                    NULL,
                    LEAST(52, GREATEST(1, COALESCE(
                        (SELECT NULLIF(regexp_replace(sc.""Value"", '[^0-9-]', '', 'g'), '')::int
                         FROM ""SystemConfigs"" sc WHERE sc.""Key"" = 'Attendance.TermWeekCount'), 14))),
                    LEAST(100, GREATEST(0, COALESCE(
                        (SELECT NULLIF(regexp_replace(sc.""Value"", '[^0-9.\-]', '', 'g'), '')::numeric
                         FROM ""SystemConfigs"" sc WHERE sc.""Key"" = 'Attendance.TheoryAbsenceLimitPercent'), 30))),
                    LEAST(100, GREATEST(0, COALESCE(
                        (SELECT NULLIF(regexp_replace(sc.""Value"", '[^0-9.\-]', '', 'g'), '')::numeric
                         FROM ""SystemConfigs"" sc WHERE sc.""Key"" = 'Attendance.LabAbsenceLimitPercent'), 20))),
                    COALESCE(
                        (SELECT NULLIF(trim(sc.""Value""), '')
                         FROM ""SystemConfigs"" sc WHERE sc.""Key"" = 'Attendance.Method'), 'Separate'),
                    NOW()
                FROM ""AcademicTerms"" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""TermAttendancePolicies"" p
                    WHERE p.""TermId"" = t.""Id"" AND p.""ProgramId"" IS NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TermAttendancePolicies");
        }
    }
}
