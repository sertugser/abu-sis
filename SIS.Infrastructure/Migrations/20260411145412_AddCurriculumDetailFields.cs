using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourseCategory",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupCode",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInGpa",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OpeningReason",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnTranscript",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DecisionDate",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNo",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Curricula",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradingSystem",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InternshipDuration",
                table: "Curricula",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Curricula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Curricula",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "Courses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamType",
                table: "Courses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseCategory",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "GroupCode",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "IncludeInGpa",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "OpeningReason",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ShowOnTranscript",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "DecisionDate",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "DecisionNo",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "GradingSystem",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "InternshipDuration",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ExamType",
                table: "Courses");
        }
    }
}
