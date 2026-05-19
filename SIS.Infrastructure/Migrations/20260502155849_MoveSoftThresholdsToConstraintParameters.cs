using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveSoftThresholdsToConstraintParameters : Migration
    {
        // Soft kısıt eşikleri (S1 doluluk min/max, S2 günlük yük, S3 slot yığılması,
        // S4 ortak kayıt, S7 öğrenci boşluğu) tek otorite olarak ConstraintDefinition.Parameters
        // JSON'unda yönetilecek. SchedulingAlgorithmConfigs tablosundaki 6 satır siliniyor;
        // mevcut Parameters JSON'larındaki eski key'ler (MinOccupancy, MaxOccupancy, MaxDailyHours,
        // MaxSectionsPerSlot, MinCoPairThreshold, MaxGapSlots) backend property adlarıyla
        // (S1MinOccupancy, S1MaxOccupancy, vb.) eşlensin diye yeniden yazılıyor.
        //
        // Bug fix: Önceki Parameters JSON key'leri SchedulingConstraintConfig property
        // adlarıyla eşleşmiyordu, MergeParameterOverride bunları sessizce ignore ediyordu.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) ConstraintDefinitions.Parameters JSON'larını backend property'leriyle eşleştir.
            migrationBuilder.Sql("""
                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"S1MinOccupancy": 60, "S1MaxOccupancy": 85}'
                WHERE "Code" = 'S1';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"S2MaxDailyHours": 6}'
                WHERE "Code" = 'S2';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"S3MaxSectionsPerSlot": 5}'
                WHERE "Code" = 'S3';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"S4MinCoPairThreshold": 20}'
                WHERE "Code" = 'S4';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"S7MaxGapSlots": 3}'
                WHERE "Code" = 'S7';
                """);

            // 2) SchedulingAlgorithmConfigs'tan 6 soft eşik kaldır — admin artık bu değerleri
            // ilgili kısıt kartının expand alanından düzenliyor.
            migrationBuilder.Sql("""
                DELETE FROM "SchedulingAlgorithmConfigs"
                WHERE "Key" IN (
                    'Classroom.OccupancyTarget.Min',
                    'Classroom.OccupancyTarget.Max',
                    'SoftConstraint.S2.MaxDailyHours',
                    'SoftConstraint.S3.MaxSectionsPerSlot',
                    'SoftConstraint.S4.MinCoPairThreshold',
                    'SoftConstraint.S7.MaxGapSlots'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: 6 satırı SchedulingAlgorithmConfigs'a yeniden ekle, eski JSON
            // şekillerini Parameters'a yaz.
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingAlgorithmConfigs" ("Key", "Value", "Description", "Category", "DataType")
                VALUES
                    ('Classroom.OccupancyTarget.Min', '60', 'Bir derslik en az yüzde kaç dolu olmalı (S1 hedefi). Daha düşük doluluk soft penalty alır.', 'SoftThreshold', 'int'),
                    ('Classroom.OccupancyTarget.Max', '85', 'Bir derslik en fazla yüzde kaç dolu olmalı (S1 hedefi). Aşırı dolu sınıflar da penalty alır.', 'SoftThreshold', 'int'),
                    ('SoftConstraint.S2.MaxDailyHours', '6', 'Bir öğretim üyesinin günde kaç saatten fazla ders almaması ideal. Aşılan her saat penalty üretir.', 'SoftThreshold', 'int'),
                    ('SoftConstraint.S3.MaxSectionsPerSlot', '5', 'Aynı saat diliminde en fazla kaç şube olabilir (prime-time yığılması). Aşılan her şube penalty alır.', 'SoftThreshold', 'int'),
                    ('SoftConstraint.S4.MinCoPairThreshold', '20', 'Geçmiş kayıtlara göre kaç öğrenciyi etkileyen ders çiftleri ''sık birlikte alınan'' sayılır. Eşik altında çakışma penalty üretmez.', 'SoftThreshold', 'int'),
                    ('SoftConstraint.S7.MaxGapSlots', '3', 'Öğrencinin gün içinde kaç ardışık boş slota toleransı var. Aşan her boşluk penalty üretir.', 'SoftThreshold', 'int');

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"MinOccupancy": 60, "MaxOccupancy": 85, "Weight": 3}'
                WHERE "Code" = 'S1';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"MaxDailyHours": 6, "Weight": 2}'
                WHERE "Code" = 'S2';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"MaxSectionsPerSlot": 5, "Weight": 2}'
                WHERE "Code" = 'S3';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"MinCoPairThreshold": 20, "Weight": 3}'
                WHERE "Code" = 'S4';

                UPDATE "ConstraintDefinitions"
                SET "Parameters" = '{"MaxGapSlots": 3, "Weight": 2}'
                WHERE "Code" = 'S7';
                """);
        }
    }
}
