using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRegulationRelativeGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvaluationType",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RelativeAbsoluteFloor",
                table: "ExamRegulations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RelativeInclusionLimit",
                table: "ExamRegulations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RelativeMinStudentCount",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "TAAMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 70);

            migrationBuilder.AddColumn<int>(
                name: "TBAMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 63);

            migrationBuilder.AddColumn<int>(
                name: "TBBMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 57);

            migrationBuilder.AddColumn<int>(
                name: "TCBMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "TCCMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 45);

            migrationBuilder.AddColumn<int>(
                name: "TDCMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 40);

            migrationBuilder.AddColumn<int>(
                name: "TDDMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 35);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationType",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "RelativeAbsoluteFloor",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "RelativeInclusionLimit",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "RelativeMinStudentCount",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TAAMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TBAMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TBBMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TCBMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TCCMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TDCMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "TDDMin",
                table: "ExamRegulations");
        }
    }
}
