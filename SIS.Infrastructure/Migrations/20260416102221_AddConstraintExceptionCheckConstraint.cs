using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstraintExceptionCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "TermClosingProgresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartedByUserId",
                table: "TermClosingProgresses",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                ALTER TABLE ""ConstraintExceptions""
                ADD CONSTRAINT ""CK_ConstraintException_AtLeastOneEntity""
                CHECK (
                    ""InstructorId"" IS NOT NULL OR
                    ""StudentId"" IS NOT NULL OR
                    ""DepartmentId"" IS NOT NULL OR
                    ""CourseSectionId"" IS NOT NULL OR
                    ""TermId"" IS NOT NULL
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ConstraintExceptions""
                DROP CONSTRAINT IF EXISTS ""CK_ConstraintException_AtLeastOneEntity"";
            ");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "TermClosingProgresses");

            migrationBuilder.DropColumn(
                name: "StartedByUserId",
                table: "TermClosingProgresses");
        }
    }
}
