using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetStudentYearToAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetStudentYear",
                table: "InternalAnnouncements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetStudentYear",
                table: "InternalAnnouncements",
                column: "TargetStudentYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InternalAnnouncements_TargetStudentYear",
                table: "InternalAnnouncements");

            migrationBuilder.DropColumn(
                name: "TargetStudentYear",
                table: "InternalAnnouncements");
        }
    }
}
