using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectiveGroupDepartmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "ElectiveGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_DepartmentId",
                table: "ElectiveGroups",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectiveGroups_Departments_DepartmentId",
                table: "ElectiveGroups",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectiveGroups_Departments_DepartmentId",
                table: "ElectiveGroups");

            migrationBuilder.DropIndex(
                name: "IX_ElectiveGroups_DepartmentId",
                table: "ElectiveGroups");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ElectiveGroups");
        }
    }
}
