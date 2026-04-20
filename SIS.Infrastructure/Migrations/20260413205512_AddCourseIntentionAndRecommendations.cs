using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseIntentionAndRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvisorCourseConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    AdvisorId = table.Column<int>(type: "integer", nullable: false),
                    ConstraintType = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorCourseConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Instructors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseIntentionSurveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    ManualOverrideAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatusChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseIntentionSurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseIntentionSurveys_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseIntentions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Intention = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseIntentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseIntentions_CourseIntentionSurveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "CourseIntentionSurveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseIntentions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseIntentions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentSurveyPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    PreferredDays = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreferredTimeRanges = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSurveyPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSurveyPreferences_CourseIntentionSurveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "CourseIntentionSurveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentSurveyPreferences_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_AdvisorId",
                table: "AdvisorCourseConstraints",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_CourseId",
                table: "AdvisorCourseConstraints",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_StudentId_CourseId_TermId",
                table: "AdvisorCourseConstraints",
                columns: new[] { "StudentId", "CourseId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_StudentId_TermId_IsActive",
                table: "AdvisorCourseConstraints",
                columns: new[] { "StudentId", "TermId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_TermId",
                table: "AdvisorCourseConstraints",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseIntentions_CourseId",
                table: "CourseIntentions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseIntentions_StudentId_SurveyId",
                table: "CourseIntentions",
                columns: new[] { "StudentId", "SurveyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseIntentions_SurveyId_StudentId_CourseId",
                table: "CourseIntentions",
                columns: new[] { "SurveyId", "StudentId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseIntentionSurveys_TermId",
                table: "CourseIntentionSurveys",
                column: "TermId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSurveyPreferences_StudentId",
                table: "StudentSurveyPreferences",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSurveyPreferences_SurveyId_StudentId",
                table: "StudentSurveyPreferences",
                columns: new[] { "SurveyId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisorCourseConstraints");

            migrationBuilder.DropTable(
                name: "CourseIntentions");

            migrationBuilder.DropTable(
                name: "StudentSurveyPreferences");

            migrationBuilder.DropTable(
                name: "CourseIntentionSurveys");
        }
    }
}
