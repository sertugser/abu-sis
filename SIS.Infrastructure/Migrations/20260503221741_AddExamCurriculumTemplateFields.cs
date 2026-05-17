using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamCurriculumTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationMethod",
                table: "GradingCriteria",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Absolute");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "GradeComponents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ExamType",
                table: "GradeComponents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "GradeComponents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumScore",
                table: "GradeComponents",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrerequisiteGradeComponentId",
                table: "GradeComponents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAttendanceEligibility",
                table: "GradeComponents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_PrerequisiteGradeComponentId",
                table: "GradeComponents",
                column: "PrerequisiteGradeComponentId");

            migrationBuilder.AddForeignKey(
                name: "FK_GradeComponents_GradeComponents_PrerequisiteGradeComponentId",
                table: "GradeComponents",
                column: "PrerequisiteGradeComponentId",
                principalTable: "GradeComponents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GradeComponents_GradeComponents_PrerequisiteGradeComponentId",
                table: "GradeComponents");

            migrationBuilder.DropIndex(
                name: "IX_GradeComponents_PrerequisiteGradeComponentId",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "CalculationMethod",
                table: "GradingCriteria");

            migrationBuilder.DropColumn(
                name: "ExamType",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "MinimumScore",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "PrerequisiteGradeComponentId",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "RequiresAttendanceEligibility",
                table: "GradeComponents");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "GradeComponents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
