using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributionConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionType = table.Column<int>(type: "integer", nullable: false),
                    PreferenceLevel = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionConstraints_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionConstraints_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DistributionConstraintCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionConstraintId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionConstraintCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_DistributionConstraints_Distr~",
                        column: x => x.DistributionConstraintId,
                        principalTable: "DistributionConstraints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_CourseId",
                table: "DistributionConstraintCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_CourseSectionId",
                table: "DistributionConstraintCourses",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_DistributionConstraintId_Cour~",
                table: "DistributionConstraintCourses",
                columns: new[] { "DistributionConstraintId", "CourseId", "CourseSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraints_DepartmentId",
                table: "DistributionConstraints",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraints_TermId",
                table: "DistributionConstraints",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionConstraintCourses");

            migrationBuilder.DropTable(
                name: "DistributionConstraints");
        }
    }
}
