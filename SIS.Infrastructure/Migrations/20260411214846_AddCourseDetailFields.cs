using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Courses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "Courses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Courses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "NationalCredit",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Courses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TeachingMethod",
                table: "Courses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Fix existing rows that got defaultValue: 0 (invalid enum)
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"Language\" = 1 WHERE \"Language\" = 0;");
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"TeachingMethod\" = 1 WHERE \"TeachingMethod\" = 0;");
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"Status\" = 1 WHERE \"Status\" = 0;");
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"Level\" = 1 WHERE \"Level\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "NationalCredit",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TeachingMethod",
                table: "Courses");
        }
    }
}
