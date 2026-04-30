using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseLookupsAndRefactorCurriculumCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseCategory",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "OpeningReason",
                table: "CurriculumCourses");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "CurriculumCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpeningReasonId",
                table: "CurriculumCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumCourseOpeningReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AllowsAdvisorGrading = table.Column<bool>(type: "boolean", nullable: false),
                    BypassScheduleConflict = table.Column<bool>(type: "boolean", nullable: false),
                    BypassMandatoryAttendance = table.Column<bool>(type: "boolean", nullable: false),
                    IsExcludedFromGpa = table.Column<bool>(type: "boolean", nullable: false),
                    IsCreditFree = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsCustomGradingScale = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumCourseOpeningReasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_CategoryId",
                table: "CurriculumCourses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_OpeningReasonId",
                table: "CurriculumCourses",
                column: "OpeningReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCategories_Code",
                table: "CourseCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourseOpeningReasons_Code",
                table: "CurriculumCourseOpeningReasons",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumCourses_CourseCategories_CategoryId",
                table: "CurriculumCourses",
                column: "CategoryId",
                principalTable: "CourseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumCourses_CurriculumCourseOpeningReasons_OpeningRea~",
                table: "CurriculumCourses",
                column: "OpeningReasonId",
                principalTable: "CurriculumCourseOpeningReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumCourses_CourseCategories_CategoryId",
                table: "CurriculumCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumCourses_CurriculumCourseOpeningReasons_OpeningRea~",
                table: "CurriculumCourses");

            migrationBuilder.DropTable(
                name: "CourseCategories");

            migrationBuilder.DropTable(
                name: "CurriculumCourseOpeningReasons");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumCourses_CategoryId",
                table: "CurriculumCourses");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumCourses_OpeningReasonId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "OpeningReasonId",
                table: "CurriculumCourses");

            migrationBuilder.AddColumn<string>(
                name: "CourseCategory",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningReason",
                table: "CurriculumCourses",
                type: "text",
                nullable: true);
        }
    }
}
