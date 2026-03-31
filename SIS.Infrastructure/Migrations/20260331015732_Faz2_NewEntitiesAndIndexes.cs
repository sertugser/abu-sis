using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Faz2_NewEntitiesAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingAuditLogs_CourseSections_SectionId",
                table: "SchedulingAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_GradeComponents_CourseId",
                table: "GradeComponents");

            migrationBuilder.DropIndex(
                name: "IX_Curricula_ProgramId",
                table: "Curricula");

            migrationBuilder.DropIndex(
                name: "IX_CourseSections_CourseId",
                table: "CourseSections");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Instructors",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Instructors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Faculties",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Faculties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Departments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Curricula",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Curricula",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CourseSections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SectionNumber",
                table: "CourseSections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CourseSections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Courses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Courses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvisorNote",
                table: "CourseRegistrations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationPlanId",
                table: "CourseRegistrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingName",
                table: "Classrooms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Classrooms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Classrooms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AcademicTerms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AcademicPrograms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AcademicPrograms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomTermAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomTermAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseAccessPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAccessPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    FromYear = table.Column<int>(type: "integer", nullable: false),
                    ToYear = table.Column<int>(type: "integer", nullable: false),
                    MinCgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    MinEcts = table.Column<int>(type: "integer", nullable: true),
                    MinCourseCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionRules_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdvisorNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationPlans_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrationPlans_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectionSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_TimeSlots_TimeSlotId",
                        column: x => x.TimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_CourseId_AcademicTermId_Name",
                table: "GradeComponents",
                columns: new[] { "CourseId", "AcademicTermId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_ProgramId_Version",
                table: "Curricula",
                columns: new[] { "ProgramId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId_TermId",
                table: "CourseSections",
                columns: new[] { "CourseId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId_TermId_SectionNumber",
                table: "CourseSections",
                columns: new[] { "CourseId", "TermId", "SectionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_RegistrationPlanId",
                table: "CourseRegistrations",
                column: "RegistrationPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_StudentId",
                table: "CourseRegistrations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_Name",
                table: "Classrooms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_ClassroomId",
                table: "ClassroomTermAssignments",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_FacultyId",
                table: "ClassroomTermAssignments",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_TermId_ClassroomId",
                table: "ClassroomTermAssignments",
                columns: new[] { "TermId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_CourseId",
                table: "CourseAccessPolicies",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_DepartmentId",
                table: "CourseAccessPolicies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_FacultyId",
                table: "CourseAccessPolicies",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_TermId",
                table: "CourseAccessPolicies",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_ProgramId_FromYear_ToYear",
                table: "PromotionRules",
                columns: new[] { "ProgramId", "FromYear", "ToYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPlans_StudentId_TermId",
                table: "RegistrationPlans",
                columns: new[] { "StudentId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPlans_TermId",
                table: "RegistrationPlans",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_ClassroomId",
                table: "SectionSchedules",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId",
                table: "SectionSchedules",
                columns: new[] { "CourseSectionId", "TimeSlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_TimeSlotId",
                table: "SectionSchedules",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_Key",
                table: "SystemConfigs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_Pattern_StartTime_EndTime",
                table: "TimeSlots",
                columns: new[] { "Pattern", "StartTime", "EndTime" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseRegistrations_RegistrationPlans_RegistrationPlanId",
                table: "CourseRegistrations",
                column: "RegistrationPlanId",
                principalTable: "RegistrationPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingAuditLogs_CourseSections_SectionId",
                table: "SchedulingAuditLogs",
                column: "SectionId",
                principalTable: "CourseSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseRegistrations_RegistrationPlans_RegistrationPlanId",
                table: "CourseRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingAuditLogs_CourseSections_SectionId",
                table: "SchedulingAuditLogs");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ClassroomTermAssignments");

            migrationBuilder.DropTable(
                name: "CourseAccessPolicies");

            migrationBuilder.DropTable(
                name: "PromotionRules");

            migrationBuilder.DropTable(
                name: "RegistrationPlans");

            migrationBuilder.DropTable(
                name: "SectionSchedules");

            migrationBuilder.DropTable(
                name: "SystemConfigs");

            migrationBuilder.DropTable(
                name: "TimeSlots");

            migrationBuilder.DropIndex(
                name: "IX_GradeComponents_CourseId_AcademicTermId_Name",
                table: "GradeComponents");

            migrationBuilder.DropIndex(
                name: "IX_Curricula_ProgramId_Version",
                table: "Curricula");

            migrationBuilder.DropIndex(
                name: "IX_CourseSections_CourseId_TermId",
                table: "CourseSections");

            migrationBuilder.DropIndex(
                name: "IX_CourseSections_CourseId_TermId_SectionNumber",
                table: "CourseSections");

            migrationBuilder.DropIndex(
                name: "IX_CourseRegistrations_RegistrationPlanId",
                table: "CourseRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_CourseRegistrations_StudentId",
                table: "CourseRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_Name",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Faculties");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Faculties");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "SectionNumber",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "AdvisorNote",
                table: "CourseRegistrations");

            migrationBuilder.DropColumn(
                name: "RegistrationPlanId",
                table: "CourseRegistrations");

            migrationBuilder.DropColumn(
                name: "BuildingName",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AcademicPrograms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AcademicPrograms");

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_CourseId",
                table: "GradeComponents",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_ProgramId",
                table: "Curricula",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId",
                table: "CourseSections",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingAuditLogs_CourseSections_SectionId",
                table: "SchedulingAuditLogs",
                column: "SectionId",
                principalTable: "CourseSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
