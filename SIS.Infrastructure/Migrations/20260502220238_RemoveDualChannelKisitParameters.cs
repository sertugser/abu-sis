using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDualChannelKisitParameters : Migration
    {
        // Çift kanal temizliği: H4/H13/H15/H16 kısıtlarının parametre eşikleri tek otorite olarak
        // ConstraintDefinitions.Parameters JSON'unda kalsın. Önceden hem orada hem
        // SchedulingAlgorithmConfigs tablosunda vardı — admin iki yerden farklı değerler yazabiliyordu.
        // Yeni model: kısıt kartından düzenlenir, scheduler ConstraintEvaluationService.BuildEffectiveConfig
        // üzerinden okur. Algoritma Parametreleri tabı sadece SA/MRV/LNS algoritma ayarlarına odaklı kalır.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) H4 ve H16'nın boş Parameters'larını backend property adlarıyla doldur.
            migrationBuilder.Sql("""
                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"AmphiThreshold": 100}'
                WHERE "Code" = 'H4' AND ("Parameters" IS NULL OR "Parameters" = '');

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"TheoryLabSplitThreshold": 4}'
                WHERE "Code" = 'H16' AND ("Parameters" IS NULL OR "Parameters" = '');
                """);

            // 2) Çift kanal yaratan 5 key'i SchedulingAlgorithmConfigs'tan kaldır.
            // Bu key'ler artık ilgili kısıtın Parameters JSON'unda yaşıyor.
            migrationBuilder.Sql("""
                DELETE FROM "SchedulingAlgorithmConfigs"
                WHERE "Key" IN (
                    'Classroom.AmphiThreshold',
                    'Schedule.LunchBreakStart',
                    'Schedule.LunchBreakEnd',
                    'Schedule.MaxConsecutiveHours',
                    'Schedule.TheoryLabSplitThreshold'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: 5 key'i tablo'ya yeniden ekle, H4/H16 Parameters'larını sıfırla.
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingAlgorithmConfigs" ("Key", "Value", "Description", "Category", "DataType")
                VALUES
                    ('Classroom.AmphiThreshold', '100', 'Bir dersin kontenjanı bu sayıyı geçerse amfi zorunlu olur.', 'HardThreshold', 'int'),
                    ('Schedule.LunchBreakStart', '11:40', 'Öğle arasının başlangıç saati (HH:mm).', 'HardThreshold', 'string'),
                    ('Schedule.LunchBreakEnd', '12:20', 'Öğle arasının bitiş saati (HH:mm).', 'HardThreshold', 'string'),
                    ('Schedule.MaxConsecutiveHours', '4', 'Bir öğretim üyesi en fazla kaç saat aralıksız ders verebilir.', 'HardThreshold', 'int'),
                    ('Schedule.TheoryLabSplitThreshold', '4', 'Bir dersin teori + lab toplamı bu saati aşarsa farklı günlere bölünür.', 'Other', 'int');

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = NULL
                WHERE "Code" IN ('H4', 'H16');
                """);
        }
    }
}
