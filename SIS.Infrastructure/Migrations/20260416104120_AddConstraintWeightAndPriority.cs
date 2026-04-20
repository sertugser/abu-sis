using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstraintWeightAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ConstraintDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                table: "ConstraintDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            // Seed Priority from existing SortOrder, Weight default to 5
            migrationBuilder.Sql(@"UPDATE ""ConstraintDefinitions"" SET ""Priority"" = ""SortOrder"", ""Weight"" = 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ConstraintDefinitions");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "ConstraintDefinitions");
        }
    }
}
