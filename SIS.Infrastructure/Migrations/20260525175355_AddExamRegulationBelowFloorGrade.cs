using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRegulationBelowFloorGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BelowFloorGrade",
                table: "ExamRegulations",
                type: "integer",
                nullable: false,
                defaultValue: 7); // 7 = GradeCode.FF — existing rows get FF, matching C# default
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BelowFloorGrade",
                table: "ExamRegulations");
        }
    }
}
