using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectiveGroupEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElectiveGroupId",
                table: "CurriculumCourses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalQuota",
                table: "CourseSections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InternalQuota",
                table: "CourseSections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElectiveSubtype",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsElective",
                table: "Courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CourseAccessRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AllowedFacultyId = table.Column<int>(type: "integer", nullable: true),
                    AllowedDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    AllowedProgramId = table.Column<int>(type: "integer", nullable: true),
                    MinClassYear = table.Column<int>(type: "integer", nullable: true),
                    MaxClassYear = table.Column<int>(type: "integer", nullable: true),
                    MinCgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    RequiresAdvisorApproval = table.Column<bool>(type: "boolean", nullable: false),
                    IsOpenToOwnDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAccessRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_AcademicPrograms_AllowedProgramId",
                        column: x => x.AllowedProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Departments_AllowedDepartmentId",
                        column: x => x.AllowedDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Faculties_AllowedFacultyId",
                        column: x => x.AllowedFacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ElectiveGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ElectiveSubtype = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectiveGroups_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumElectivePools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurriculumId = table.Column<int>(type: "integer", nullable: false),
                    ElectiveGroupId = table.Column<int>(type: "integer", nullable: false),
                    SuggestedSemesterStart = table.Column<int>(type: "integer", nullable: false),
                    SuggestedSemesterEnd = table.Column<int>(type: "integer", nullable: false),
                    RequiredCourseCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredEcts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumElectivePools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumElectivePools_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurriculumElectivePools_ElectiveGroups_ElectiveGroupId",
                        column: x => x.ElectiveGroupId,
                        principalTable: "ElectiveGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectiveGroupCourses",
                columns: table => new
                {
                    ElectiveGroupId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveGroupCourses", x => new { x.ElectiveGroupId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_ElectiveGroupCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectiveGroupCourses_ElectiveGroups_ElectiveGroupId",
                        column: x => x.ElectiveGroupId,
                        principalTable: "ElectiveGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_ElectiveGroupId",
                table: "CurriculumCourses",
                column: "ElectiveGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedDepartmentId",
                table: "CourseAccessRules",
                column: "AllowedDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedFacultyId",
                table: "CourseAccessRules",
                column: "AllowedFacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedProgramId",
                table: "CourseAccessRules",
                column: "AllowedProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_CourseId",
                table: "CourseAccessRules",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId",
                table: "CurriculumElectivePools",
                columns: new[] { "CurriculumId", "ElectiveGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_ElectiveGroupId",
                table: "CurriculumElectivePools",
                column: "ElectiveGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroupCourses_CourseId",
                table: "ElectiveGroupCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_Code",
                table: "ElectiveGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_FacultyId",
                table: "ElectiveGroups",
                column: "FacultyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumCourses_ElectiveGroups_ElectiveGroupId",
                table: "CurriculumCourses",
                column: "ElectiveGroupId",
                principalTable: "ElectiveGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumCourses_ElectiveGroups_ElectiveGroupId",
                table: "CurriculumCourses");

            migrationBuilder.DropTable(
                name: "CourseAccessRules");

            migrationBuilder.DropTable(
                name: "CurriculumElectivePools");

            migrationBuilder.DropTable(
                name: "ElectiveGroupCourses");

            migrationBuilder.DropTable(
                name: "ElectiveGroups");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumCourses_ElectiveGroupId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ElectiveGroupId",
                table: "CurriculumCourses");

            migrationBuilder.DropColumn(
                name: "ExternalQuota",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "InternalQuota",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "ElectiveSubtype",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsElective",
                table: "Courses");
        }
    }
}
