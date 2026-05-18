using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// 2026-05-17 — S20 (Komşu Yıl Çakışma) Soft → Hard dönüşümü.
    ///
    /// CohortBuilder artık komşu yıl zorunlu ders çiftleri için aynı-yıl section-aware
    /// mantığını (AddCoursePairConflicts) hard pair olarak uyguluyor. Sonuç: alttan/üstten
    /// ders alacak öğrenci için en az bir uyumlu şube kombinasyonu kalması GARANTİ.
    ///
    /// Mevcut DB kayıtlarındaki S20 ConstraintDefinition satırı Category='Soft' ile
    /// kalıyor — bu migration onu 'Hard' yapar + name ve description'ı yeni davranışa
    /// uygun günceller. Parameters JSON null'a düşer (Weight/PenaltyPerPair artık
    /// kullanılmıyor; hard kuralda ağırlık anlamsız).
    /// </summary>
    public partial class S20HardAdjacentYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""ConstraintDefinitions""
SET ""Category"" = 'Hard',
    ""Name"" = 'Komşu Yıl Çakışma Garantisi',
    ""Description"" = 'Komşu sınıfların (1-2, 2-3, 3-4) zorunlu dersleri arasında öğrencinin alabileceği en az bir alternatif şube kombinasyonu kalmalı. Bir öğrenci alttan kalan veya üstten almak istediği zorunlu bir dersin en az bir şubesi, kendi sınıfının zorunlu dersleriyle çakışmamalı. Seçmeli dersler bu kuralın dışında.',
    ""Parameters"" = NULL,
    ""UpdatedAt"" = NOW()
WHERE ""Code"" = 'S20';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""ConstraintDefinitions""
SET ""Category"" = 'Soft',
    ""Name"" = 'Komşu Yıl Çakışma',
    ""Description"" = 'Aynı bölüm içinde komşu yıl (1-2, 2-3, 3-4) ders çiftleri arasında çakışma penalty''si. Kalan/önden ders alan öğrenciler için section alternatifi kalmasını teşvik eder.',
    ""Parameters"" = '{""Weight"": 1, ""PenaltyPerPair"": 5.0}',
    ""UpdatedAt"" = NOW()
WHERE ""Code"" = 'S20';
");
        }
    }
}
