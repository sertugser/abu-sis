using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRegulationExamRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoTransferExcusedToMakeup",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoTransferFailedToMakeup",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlockFinalIfNoMidterm",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlockMakeupIfNoFinal",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FailIfNoFinal",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MakeupReplacesFinal",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ZeroIfMissedMakeup",
                table: "ExamRegulations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoTransferExcusedToMakeup",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "AutoTransferFailedToMakeup",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "BlockFinalIfNoMidterm",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "BlockMakeupIfNoFinal",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "FailIfNoFinal",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "MakeupReplacesFinal",
                table: "ExamRegulations");

            migrationBuilder.DropColumn(
                name: "ZeroIfMissedMakeup",
                table: "ExamRegulations");
        }
    }
}
