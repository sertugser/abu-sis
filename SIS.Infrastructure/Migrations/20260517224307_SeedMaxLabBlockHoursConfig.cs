using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// 2026-05-17 — <c>Schedule.MaxLabBlockHours</c> SystemConfigs'e eklendi.
    ///
    /// CpSatSolverEngine bu key'i okuyordu (default 2 fallback) ama key DB'de yoktu, sayfada
    /// görünmüyordu, kullanıcı değiştiremiyordu. Şimdi seed + UI satırı: default 2, aralık 2-6.
    /// </summary>
    public partial class SeedMaxLabBlockHoursConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'Schedule.MaxLabBlockHours',
    '2',
    'Lab dersi en fazla kaç saatlik bloklara bölünsün. Bu değerden uzun lab''lar otomatik parçalara ayrılıp farklı günlere dağıtılır. Örnek: 4 saatlik lab + bu değer 2 → iki ayrı günde 2''şer saat. Default 2 (çoğu üniversitede standart).',
    'Other',
    'int'
)
ON CONFLICT (""Key"") DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SystemConfigs"" WHERE ""Key"" = 'Schedule.MaxLabBlockHours';");
        }
    }
}
