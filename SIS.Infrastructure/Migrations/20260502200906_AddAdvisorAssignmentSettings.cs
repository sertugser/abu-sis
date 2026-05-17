using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisorAssignmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdvisorAssignmentCapacity",
                table: "Instructors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailableForAdvisorAssignment",
                table: "Instructors",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvisorAssignmentCapacity",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "IsAvailableForAdvisorAssignment",
                table: "Instructors");
        }
    }
}
