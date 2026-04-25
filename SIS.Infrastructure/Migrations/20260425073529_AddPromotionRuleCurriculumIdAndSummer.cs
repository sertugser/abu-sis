using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionRuleCurriculumIdAndSummer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromotionRules_ProgramId_FromYear_ToYear",
                table: "PromotionRules");

            migrationBuilder.AddColumn<int>(
                name: "CurriculumId",
                table: "PromotionRules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_CurriculumId",
                table: "PromotionRules",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_ProgramId_CurriculumId_FromYear_ToYear",
                table: "PromotionRules",
                columns: new[] { "ProgramId", "CurriculumId", "FromYear", "ToYear" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionRules_Curricula_CurriculumId",
                table: "PromotionRules",
                column: "CurriculumId",
                principalTable: "Curricula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromotionRules_Curricula_CurriculumId",
                table: "PromotionRules");

            migrationBuilder.DropIndex(
                name: "IX_PromotionRules_CurriculumId",
                table: "PromotionRules");

            migrationBuilder.DropIndex(
                name: "IX_PromotionRules_ProgramId_CurriculumId_FromYear_ToYear",
                table: "PromotionRules");

            migrationBuilder.DropColumn(
                name: "CurriculumId",
                table: "PromotionRules");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_ProgramId_FromYear_ToYear",
                table: "PromotionRules",
                columns: new[] { "ProgramId", "FromYear", "ToYear" },
                unique: true);
        }
    }
}
