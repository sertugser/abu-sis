using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropElectiveGroupElectiveSubtypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElectiveSubtype",
                table: "ElectiveGroups");

            migrationBuilder.AlterColumn<int>(
                name: "ElectiveSubtypeId",
                table: "ElectiveGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ElectiveSubtypeId",
                table: "ElectiveGroups",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ElectiveSubtype",
                table: "ElectiveGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
