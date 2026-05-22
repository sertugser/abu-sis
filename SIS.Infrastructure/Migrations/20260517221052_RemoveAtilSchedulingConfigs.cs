using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// 2026-05-17 — Algoritma Parametreleri sayfası temizliği.
    ///
    /// 1. <c>Schedule.SplittableBlockTypes</c> ve <c>Schedule.Lab.MaxBlockHours</c> atıl —
    ///    SchedulingConstraintConfig'ten property/loader silindi, hiçbir encoder okumuyor.
    ///    UI'da düzenlenebiliyordu ama davranışa etkisi yoktu (DIST Preferred ile aynı sınıf bug).
    /// 2. <c>Schedule.CurriculumConflictMode</c> Description'ı genişletildi —
    ///    UI'da artık geçerli 3 değer (cohort/curriculum/enrollment) ile bunların davranışı
    ///    görünecek; dropdown render'ı için kullanıcıya net rehber.
    /// </summary>
    public partial class RemoveAtilSchedulingConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""SystemConfigs""
WHERE ""Key"" IN ('Schedule.SplittableBlockTypes', 'Schedule.Lab.MaxBlockHours');
");

            migrationBuilder.Sql(@"
UPDATE ""SystemConfigs""
SET ""Description"" = 'H5 müfredat çakışma kontrolü modu. Cohort (önerilen): section''ların alternatif olduğunu bilir, gerçek üniversite gibi çakışma kontrolü yapar. Curriculum: section sayısını umursamaz, daha agresif (çoğu durumda UNSAT). Enrollment: henüz desteklenmiyor.'
WHERE ""Key"" = 'Schedule.CurriculumConflictMode';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'Schedule.SplittableBlockTypes',
    'Lab',
    'Bir derslik yetmediğinde hangi tip dersler aynı saatte iki ayrı dersliğe bölünebilir.',
    'Other',
    'string'
)
ON CONFLICT (""Key"") DO NOTHING;
");

            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'Schedule.Lab.MaxBlockHours',
    '2',
    'Lab dersi kaç saatlik bloklara bölünsün.',
    'Other',
    'int'
)
ON CONFLICT (""Key"") DO NOTHING;
");

            migrationBuilder.Sql(@"
UPDATE ""SystemConfigs""
SET ""Description"" = 'H5 müfredat çakışma kontrolü modu. ''cohort'' önerilen — section''ların alternatif olduğunu bilir.'
WHERE ""Key"" = 'Schedule.CurriculumConflictMode';
");
        }
    }
}
