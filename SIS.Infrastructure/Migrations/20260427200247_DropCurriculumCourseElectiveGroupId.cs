using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropCurriculumCourseElectiveGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumCourses_ElectiveGroups_ElectiveGroupId",
                table: "CurriculumCourses");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumCourses_ElectiveGroupId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ElectiveGroupId",
                table: "CurriculumCourses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElectiveGroupId",
                table: "CurriculumCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_ElectiveGroupId",
                table: "CurriculumCourses",
                column: "ElectiveGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumCourses_ElectiveGroups_ElectiveGroupId",
                table: "CurriculumCourses",
                column: "ElectiveGroupId",
                principalTable: "ElectiveGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
