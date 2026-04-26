using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumCoursePreviousCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviousCourseId",
                table: "CurriculumCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_PreviousCourseId",
                table: "CurriculumCourses",
                column: "PreviousCourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumCourses_Courses_PreviousCourseId",
                table: "CurriculumCourses",
                column: "PreviousCourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumCourses_Courses_PreviousCourseId",
                table: "CurriculumCourses");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumCourses_PreviousCourseId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "PreviousCourseId",
                table: "CurriculumCourses");
        }
    }
}
