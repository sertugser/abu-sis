using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DersIslemleriIyilestirmeFaz1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServiceUnit",
                table: "Departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EctsPerCourse",
                table: "CurriculumElectivePools",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Curricula",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Curricula",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AppliedElectivePoolId",
                table: "CourseRegistrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EctsSnapshot",
                table: "CourseRegistrations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseCrossLists",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    LinkedCourseId = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCrossLists", x => new { x.CourseId, x.LinkedCourseId });
                    table.ForeignKey(
                        name: "FK_CourseCrossLists_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseCrossLists_Courses_LinkedCourseId",
                        column: x => x.LinkedCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_AppliedElectivePoolId",
                table: "CourseRegistrations",
                column: "AppliedElectivePoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCrossLists_LinkedCourseId",
                table: "CourseCrossLists",
                column: "LinkedCourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseRegistrations_CurriculumElectivePools_AppliedElective~",
                table: "CourseRegistrations",
                column: "AppliedElectivePoolId",
                principalTable: "CurriculumElectivePools",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseRegistrations_CurriculumElectivePools_AppliedElective~",
                table: "CourseRegistrations");

            migrationBuilder.DropTable(
                name: "CourseCrossLists");

            migrationBuilder.DropIndex(
                name: "IX_CourseRegistrations_AppliedElectivePoolId",
                table: "CourseRegistrations");

            migrationBuilder.DropColumn(
                name: "IsServiceUnit",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "EctsPerCourse",
                table: "CurriculumElectivePools");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "AppliedElectivePoolId",
                table: "CourseRegistrations");

            migrationBuilder.DropColumn(
                name: "EctsSnapshot",
                table: "CourseRegistrations");
        }
    }
}
