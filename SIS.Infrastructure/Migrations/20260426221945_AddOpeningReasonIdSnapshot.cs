using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningReasonIdSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpeningReasonIdSnapshot",
                table: "CourseRegistrations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_OpeningReasonIdSnapshot",
                table: "CourseRegistrations",
                column: "OpeningReasonIdSnapshot");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseRegistrations_CurriculumCourseOpeningReasons_OpeningR~",
                table: "CourseRegistrations",
                column: "OpeningReasonIdSnapshot",
                principalTable: "CurriculumCourseOpeningReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseRegistrations_CurriculumCourseOpeningReasons_OpeningR~",
                table: "CourseRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_CourseRegistrations_OpeningReasonIdSnapshot",
                table: "CourseRegistrations");

            migrationBuilder.DropColumn(
                name: "OpeningReasonIdSnapshot",
                table: "CourseRegistrations");
        }
    }
}
