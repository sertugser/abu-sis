using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursePrerequisiteRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanBeTakenStandalone",
                table: "Courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinCreditsRequired",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinEctsRequired",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresNoFailedLowerCourses",
                table: "Courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanBeTakenStandalone",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "MinCreditsRequired",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "MinEctsRequired",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "RequiresNoFailedLowerCourses",
                table: "Courses");
        }
    }
}
