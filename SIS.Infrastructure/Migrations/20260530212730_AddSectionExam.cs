using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "GradeComponents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SectionExams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    GradeComponentId = table.Column<int>(type: "integer", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsAnnouncedToStudents = table.Column<bool>(type: "boolean", nullable: false),
                    AnnouncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionExams_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SectionExams_GradeComponents_GradeComponentId",
                        column: x => x.GradeComponentId,
                        principalTable: "GradeComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionExamClassrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionExamId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionExamClassrooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionExamClassrooms_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionExamClassrooms_SectionExams_SectionExamId",
                        column: x => x.SectionExamId,
                        principalTable: "SectionExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectionExamClassrooms_ClassroomId",
                table: "SectionExamClassrooms",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionExamClassrooms_SectionExamId_ClassroomId",
                table: "SectionExamClassrooms",
                columns: new[] { "SectionExamId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionExams_CourseSectionId_GradeComponentId",
                table: "SectionExams",
                columns: new[] { "CourseSectionId", "GradeComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionExams_GradeComponentId",
                table: "SectionExams",
                column: "GradeComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionExamClassrooms");

            migrationBuilder.DropTable(
                name: "SectionExams");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "GradeComponents");
        }
    }
}
