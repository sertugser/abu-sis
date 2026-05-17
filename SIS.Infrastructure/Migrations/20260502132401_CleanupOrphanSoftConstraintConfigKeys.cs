using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupOrphanSoftConstraintConfigKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft kısıt aç/kapat ve ağırlık değerleri artık ConstraintDefinition tablosundan
            // okunuyor (tek otorite). Aşağıdaki SystemConfig key'leri çift-kanal sorununu
            // önlemek için kaldırılıyor.
            migrationBuilder.Sql("""
                DELETE FROM "SystemConfigs"
                WHERE "Key" IN (
                    'SoftConstraint.S1.Enabled',
                    'SoftConstraint.S2.Enabled',
                    'SoftConstraint.S3.Enabled',
                    'SoftConstraint.S4.Enabled',
                    'SoftConstraint.S5.Enabled',
                    'SoftConstraint.S7.Enabled',
                    'SoftConstraint.S8.Enabled',
                    'SoftConstraint.S9.Enabled',
                    'SoftConstraint.S12.Enabled',
                    'SoftConstraint.Weight.S1',
                    'SoftConstraint.Weight.S2',
                    'SoftConstraint.Weight.S3',
                    'SoftConstraint.Weight.S4',
                    'SoftConstraint.Weight.S5',
                    'SoftConstraint.Weight.S7',
                    'SoftConstraint.Weight.S8',
                    'SoftConstraint.Weight.S9',
                    'SoftConstraint.Weight.S12'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "SystemConfigs" ("Key", "Value", "Category", "DataType", "Description")
                VALUES
                    ('SoftConstraint.S1.Enabled', 'true', 'Scheduling', 'bool', 'S1: Derslik doluluk oranı kontrolü aktif/pasif'),
                    ('SoftConstraint.S2.Enabled', 'true', 'Scheduling', 'bool', 'S2: Hoca günlük yoğunluk dengesi kontrolü aktif/pasif'),
                    ('SoftConstraint.S3.Enabled', 'true', 'Scheduling', 'bool', 'S3: Slot dağılım dengesi kontrolü aktif/pasif'),
                    ('SoftConstraint.S4.Enabled', 'true', 'Scheduling', 'bool', 'S4: Cross-department sık birlikte alınan ders çakışma uyarısı aktif/pasif'),
                    ('SoftConstraint.S5.Enabled', 'true', 'Scheduling', 'bool', 'S5: Öğle arası koruması (soft mod) aktif/pasif'),
                    ('SoftConstraint.S7.Enabled', 'true', 'Scheduling', 'bool', 'S7: Öğrenci ders arası boşluk kontrolü aktif/pasif'),
                    ('SoftConstraint.S8.Enabled', 'true', 'Scheduling', 'bool', 'S8: Hoca gün sayısı minimizasyonu aktif/pasif'),
                    ('SoftConstraint.S9.Enabled', 'true', 'Scheduling', 'bool', 'S9: Erken sabah/geç akşam cezası aktif/pasif'),
                    ('SoftConstraint.S12.Enabled', 'true', 'Scheduling', 'bool', 'S12: Hafta içi dengeli dağılım aktif/pasif'),
                    ('SoftConstraint.Weight.S1', '3', 'Scheduling', 'decimal', 'S1 ağırlığı'),
                    ('SoftConstraint.Weight.S2', '2', 'Scheduling', 'decimal', 'S2 ağırlığı'),
                    ('SoftConstraint.Weight.S3', '2', 'Scheduling', 'decimal', 'S3 ağırlığı'),
                    ('SoftConstraint.Weight.S4', '3', 'Scheduling', 'decimal', 'S4 ağırlığı'),
                    ('SoftConstraint.Weight.S5', '2', 'Scheduling', 'decimal', 'S5 ağırlığı'),
                    ('SoftConstraint.Weight.S7', '2', 'Scheduling', 'decimal', 'S7 ağırlığı'),
                    ('SoftConstraint.Weight.S8', '1', 'Scheduling', 'decimal', 'S8 ağırlığı'),
                    ('SoftConstraint.Weight.S9', '1', 'Scheduling', 'decimal', 'S9 ağırlığı'),
                    ('SoftConstraint.Weight.S12', '1', 'Scheduling', 'decimal', 'S12 ağırlığı');
                """);
        }
    }
}
