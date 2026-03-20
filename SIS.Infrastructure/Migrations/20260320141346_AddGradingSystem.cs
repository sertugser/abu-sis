using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFinalized",
                table: "CourseSections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GradeComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AcademicTermId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeComponents_AcademicTerms_AcademicTermId",
                        column: x => x.AcademicTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeComponents_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AcademicTermId = table.Column<int>(type: "integer", nullable: false),
                    AAMin = table.Column<int>(type: "integer", nullable: false),
                    BAMin = table.Column<int>(type: "integer", nullable: false),
                    BBMin = table.Column<int>(type: "integer", nullable: false),
                    CBMin = table.Column<int>(type: "integer", nullable: false),
                    CCMin = table.Column<int>(type: "integer", nullable: false),
                    DCMin = table.Column<int>(type: "integer", nullable: false),
                    DDMin = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingCriteria_AcademicTerms_AcademicTermId",
                        column: x => x.AcademicTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingCriteria_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    GradeComponentId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentGrades_CourseRegistrations_CourseRegistrationId",
                        column: x => x.CourseRegistrationId,
                        principalTable: "CourseRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGrades_GradeComponents_GradeComponentId",
                        column: x => x.GradeComponentId,
                        principalTable: "GradeComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_AcademicTermId",
                table: "GradeComponents",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_CourseId",
                table: "GradeComponents",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingCriteria_AcademicTermId",
                table: "GradingCriteria",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingCriteria_CourseId_AcademicTermId",
                table: "GradingCriteria",
                columns: new[] { "CourseId", "AcademicTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGrades_CourseRegistrationId_GradeComponentId",
                table: "StudentGrades",
                columns: new[] { "CourseRegistrationId", "GradeComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGrades_GradeComponentId",
                table: "StudentGrades",
                column: "GradeComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradingCriteria");

            migrationBuilder.DropTable(
                name: "StudentGrades");

            migrationBuilder.DropTable(
                name: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "IsFinalized",
                table: "CourseSections");
        }
    }
}
