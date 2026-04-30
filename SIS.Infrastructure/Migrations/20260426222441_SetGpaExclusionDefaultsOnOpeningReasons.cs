using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// Faz 4 — Mevcut DB'deki standart Açma Nedenleri için IsExcludedFromGpa default'unu set et.
    /// Hobi/Staj/Tez/Uzmanlık Alan/Seminer/Danışmanlık dersleri akademik standart gereği GPA dışı.
    /// Sadece bu satırları true yapar; diğerleri dokunulmaz. Yeni kurulumlarda seed'de set edilir.
    /// </summary>
    public partial class SetGpaExclusionDefaultsOnOpeningReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CurriculumCourseOpeningReasons""
                SET ""IsExcludedFromGpa"" = TRUE
                WHERE ""Code"" IN ('Hobby', 'Internship', 'Thesis', 'ExpertiseField', 'Seminar', 'Counseling');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CurriculumCourseOpeningReasons""
                SET ""IsExcludedFromGpa"" = FALSE
                WHERE ""Code"" IN ('Hobby', 'Internship', 'Thesis', 'ExpertiseField', 'Seminar', 'Counseling');
            ");
        }
    }
}
