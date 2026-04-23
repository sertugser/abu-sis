using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumEctsOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EctsLimit200To249",
                table: "Curricula",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EctsLimitAbove250",
                table: "Curricula",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EctsLimitBelow200",
                table: "Curricula",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EctsLimit200To249",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "EctsLimitAbove250",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "EctsLimitBelow200",
                table: "Curricula");
        }
    }
}
