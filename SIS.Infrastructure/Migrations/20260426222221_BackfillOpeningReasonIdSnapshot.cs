using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// Faz 4 — Mevcut CourseRegistration kayıtlarına OpeningReasonIdSnapshot backfill.
    /// Öğrencinin aktif Main programının curriculum'undaki CurriculumCourse satırından
    /// OpeningReasonId çekilerek snapshot'a yazılır. Match yoksa null kalır (havuz dersleri,
    /// müfredat dışı kayıtlar, programsız öğrenciler — yeni davranış etkili olmaz).
    /// </summary>
    public partial class BackfillOpeningReasonIdSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CourseRegistrations"" cr
                SET ""OpeningReasonIdSnapshot"" = cc.""OpeningReasonId""
                FROM ""CourseSections"" cs,
                     ""StudentPrograms"" sp,
                     ""CurriculumCourses"" cc
                WHERE cr.""CourseSectionId"" = cs.""Id""
                  AND sp.""StudentId"" = cr.""StudentId""
                  AND sp.""Type"" = 0
                  AND sp.""IsActive"" = TRUE
                  AND cc.""CurriculumId"" = sp.""CurriculumId""
                  AND cc.""CourseId"" = cs.""CourseId""
                  AND cr.""OpeningReasonIdSnapshot"" IS NULL
                  AND cc.""OpeningReasonId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill geri alınır — tüm snapshot null'a çekilir
            migrationBuilder.Sql(@"
                UPDATE ""CourseRegistrations""
                SET ""OpeningReasonIdSnapshot"" = NULL;
            ");
        }
    }
}
