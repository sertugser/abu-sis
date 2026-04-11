using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionRuleNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxFailedCourses",
                table: "PromotionRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinCompulsoryCoursePassed",
                table: "PromotionRules",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxFailedCourses",
                table: "PromotionRules");

            migrationBuilder.DropColumn(
                name: "MinCompulsoryCoursePassed",
                table: "PromotionRules");
        }
    }
}
