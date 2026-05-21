using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSlotTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresentSlotNumbersCsv",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalSlotCount",
                table: "AttendanceRecords",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentSlotNumbersCsv",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "TotalSlotCount",
                table: "AttendanceRecords");
        }
    }
}
