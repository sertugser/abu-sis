using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRegulationLetterRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AAMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BAMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BBMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CBMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CCMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DCMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DDMin",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LetterRangesEditable",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AAMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "BAMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "BBMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "CBMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "CCMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "DCMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "DDMin",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "LetterRangesEditable",
                table: "ExamRegulations");
        }
    }
}
