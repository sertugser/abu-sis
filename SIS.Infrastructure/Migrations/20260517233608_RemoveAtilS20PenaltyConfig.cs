using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// 2026-05-17 — <c>SoftConstraint.S20.PenaltyPerPair</c> SystemConfigs'ten kaldırıldı.
    ///
    /// S20 (Komşu Yıl Çakışma) Soft → Hard dönüşümü ile ceza puanı anlamsızlaştı:
    ///   - Soft path (AdjacentYearEncoder) artık çağrılan veri (AdjacentYearSoftPairs) boş
    ///   - Hard path (CohortBuilder ConflictPairs) zaten section-aware NoOverlap uyguluyor
    /// Algoritma Parametreleri sayfasında atıl bir satır olarak kalmıyordu — kullanıcı
    /// "5'i 10 yapsam değişir mi?" sorusunu sorabilir, cevap "hayır, hiç dokunulmuyor"
    /// olmalıydı. UI'dan tamamen kaldırmak en temiz çözüm.
    /// </summary>
    public partial class RemoveAtilS20PenaltyConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SystemConfigs"" WHERE ""Key"" = 'SoftConstraint.S20.PenaltyPerPair';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'SoftConstraint.S20.PenaltyPerPair',
    '5.0',
    'Komşu sınıfların dersleri (örn. 2. sınıfın bir dersi + 3. sınıfın bir dersi) aynı saate konursa programa eklenecek ceza puanı.',
    'Soft',
    'decimal'
)
ON CONFLICT (""Key"") DO NOTHING;
");
        }
    }
}
