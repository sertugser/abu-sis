using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaceholderElectiveCourses : Migration
    {
        // Placeholder course codes that were used as fake elective slots
        private static readonly string[] PlaceholderCodes =
        {
            "TE-I", "TE-II", "TE-III", "TE-IV", "TE-V", "TE-VI", "TE-VII",
            "OC-I", "OC-II",
            "NDE-I", "NDE-II", "NDE-III"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Remove CurriculumCourse links that reference placeholder courses
            migrationBuilder.Sql("""
                DELETE FROM "CurriculumCourses"
                WHERE "CourseId" IN (
                    SELECT "Id" FROM "Courses"
                    WHERE "Code" IN ('TE-I','TE-II','TE-III','TE-IV','TE-V','TE-VI','TE-VII',
                                     'OC-I','OC-II','NDE-I','NDE-II','NDE-III')
                );
                """);

            // 2. Remove placeholder courses themselves
            migrationBuilder.Sql("""
                DELETE FROM "Courses"
                WHERE "Code" IN ('TE-I','TE-II','TE-III','TE-IV','TE-V','TE-VI','TE-VII',
                                 'OC-I','OC-II','NDE-I','NDE-II','NDE-III');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migration — no automatic rollback.
            // Re-seed the database to restore placeholder courses if needed.
        }
    }
}
