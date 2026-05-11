using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleImportSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    RowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleImportSessions_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleImportSessions_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_ConsumedAt",
                table: "ScheduleImportSessions",
                column: "ConsumedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_CreatedByUserId_FacultyId_TermId_Exp~",
                table: "ScheduleImportSessions",
                columns: new[] { "CreatedByUserId", "FacultyId", "TermId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_FacultyId",
                table: "ScheduleImportSessions",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_TermId",
                table: "ScheduleImportSessions",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleImportSessions");
        }
    }
}
