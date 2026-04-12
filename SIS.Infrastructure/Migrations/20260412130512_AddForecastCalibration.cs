using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForecastCalibrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    PredictedDemand = table.Column<int>(type: "integer", nullable: false),
                    ActualEnrollment = table.Column<int>(type: "integer", nullable: false),
                    PredictedSections = table.Column<int>(type: "integer", nullable: false),
                    ActualSections = table.Column<int>(type: "integer", nullable: false),
                    AbsoluteError = table.Column<decimal>(type: "numeric", nullable: false),
                    PercentageError = table.Column<decimal>(type: "numeric", nullable: false),
                    RuleBasedWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    HistoricalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    EligibleStudentCount = table.Column<int>(type: "integer", nullable: false),
                    ShowRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CalibratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastCalibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_CourseId_TermId",
                table: "ForecastCalibrations",
                columns: new[] { "CourseId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_DepartmentId",
                table: "ForecastCalibrations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_TermId",
                table: "ForecastCalibrations",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForecastCalibrations");
        }
    }
}
