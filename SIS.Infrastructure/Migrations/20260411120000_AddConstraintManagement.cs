using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260411120000_AddConstraintManagement")]
public partial class AddConstraintManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConstraintDefinitions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                Parameters = table.Column<string>(type: "text", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConstraintDefinitions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConstraintDefinitions_Code",
            table: "ConstraintDefinitions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateTable(
            name: "ConstraintExceptions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConstraintDefinitionId = table.Column<int>(type: "integer", nullable: false),
                InstructorId = table.Column<int>(type: "integer", nullable: true),
                StudentId = table.Column<int>(type: "integer", nullable: true),
                DepartmentId = table.Column<int>(type: "integer", nullable: true),
                CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                TermId = table.Column<int>(type: "integer", nullable: true),
                IsExempt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ParameterOverride = table.Column<string>(type: "text", nullable: true),
                Reason = table.Column<string>(type: "text", nullable: true),
                CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConstraintExceptions", x => x.Id);
                table.ForeignKey("FK_ConstraintExceptions_ConstraintDefinitions", x => x.ConstraintDefinitionId, "ConstraintDefinitions", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ConstraintExceptions_Instructors", x => x.InstructorId, "Instructors", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ConstraintExceptions_Students", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ConstraintExceptions_Departments", x => x.DepartmentId, "Departments", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ConstraintExceptions_CourseSections", x => x.CourseSectionId, "CourseSections", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ConstraintExceptions_AcademicTerms", x => x.TermId, "AcademicTerms", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConstraintExceptions_Composite",
            table: "ConstraintExceptions",
            columns: new[] { "ConstraintDefinitionId", "InstructorId", "DepartmentId", "CourseSectionId", "TermId" });

        migrationBuilder.CreateIndex(name: "IX_ConstraintExceptions_InstructorId", table: "ConstraintExceptions", column: "InstructorId");
        migrationBuilder.CreateIndex(name: "IX_ConstraintExceptions_StudentId", table: "ConstraintExceptions", column: "StudentId");
        migrationBuilder.CreateIndex(name: "IX_ConstraintExceptions_DepartmentId", table: "ConstraintExceptions", column: "DepartmentId");
        migrationBuilder.CreateIndex(name: "IX_ConstraintExceptions_CourseSectionId", table: "ConstraintExceptions", column: "CourseSectionId");
        migrationBuilder.CreateIndex(name: "IX_ConstraintExceptions_TermId", table: "ConstraintExceptions", column: "TermId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ConstraintExceptions");
        migrationBuilder.DropTable(name: "ConstraintDefinitions");
    }
}
