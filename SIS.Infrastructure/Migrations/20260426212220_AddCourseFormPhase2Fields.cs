using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseFormPhase2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoAddOnNewRegistration",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CurriculumDescription",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionDate",
                table: "CurriculumCourses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNo",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromCreditTotal",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromTranscriptTotal",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasCustomExamCurriculum",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFormationCourse",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMainGroupCourse",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAtTakenCurriculum",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInstructorAtRegistration",
                table: "CurriculumCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AlternativeName",
                table: "Courses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationHours",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenType",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInInfoPackage",
                table: "Courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAddOnNewRegistration",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "CurriculumDescription",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "DecisionDate",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "DecisionNo",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ExcludedFromCreditTotal",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ExcludedFromTranscriptTotal",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "HasCustomExamCurriculum",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "IsFormationCourse",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "IsMainGroupCourse",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ShowAtTakenCurriculum",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ShowInstructorAtRegistration",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "AlternativeName",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ApplicationHours",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "OpenType",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ShowInInfoPackage",
                table: "Courses");
        }
    }
}
