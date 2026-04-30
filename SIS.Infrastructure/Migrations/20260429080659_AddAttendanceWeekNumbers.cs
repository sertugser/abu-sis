using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceWeekNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceSessions_CourseSectionId_SessionDate",
                table: "AttendanceSessions");

            migrationBuilder.AddColumn<int>(
                name: "WeekNumber",
                table: "AttendanceSessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "AttendanceSessions" AS target
                SET "WeekNumber" = ranked."WeekNumber"
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "CourseSectionId"
                               ORDER BY "SessionDate", "Id"
                           ) AS "WeekNumber"
                    FROM "AttendanceSessions"
                ) AS ranked
                WHERE target."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CourseSectionId_WeekNumber",
                table: "AttendanceSessions",
                columns: new[] { "CourseSectionId", "WeekNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceSessions_CourseSectionId_WeekNumber",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "WeekNumber",
                table: "AttendanceSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CourseSectionId_SessionDate",
                table: "AttendanceSessions",
                columns: new[] { "CourseSectionId", "SessionDate" },
                unique: true);
        }
    }
}
