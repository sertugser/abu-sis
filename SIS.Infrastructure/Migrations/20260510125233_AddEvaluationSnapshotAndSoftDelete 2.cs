using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationSnapshotAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EvaluationQuestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EvaluationPeriodQuestions",
                columns: table => new
                {
                    EvaluationPeriodId = table.Column<int>(type: "integer", nullable: false),
                    EvaluationQuestionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationPeriodQuestions", x => new { x.EvaluationPeriodId, x.EvaluationQuestionId });
                    table.ForeignKey(
                        name: "FK_EvaluationPeriodQuestions_EvaluationPeriods_EvaluationPerio~",
                        column: x => x.EvaluationPeriodId,
                        principalTable: "EvaluationPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationPeriodQuestions_EvaluationQuestions_EvaluationQue~",
                        column: x => x.EvaluationQuestionId,
                        principalTable: "EvaluationQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationPeriodQuestions_EvaluationQuestionId",
                table: "EvaluationPeriodQuestions",
                column: "EvaluationQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationPeriodQuestions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EvaluationQuestions");
        }
    }
}
