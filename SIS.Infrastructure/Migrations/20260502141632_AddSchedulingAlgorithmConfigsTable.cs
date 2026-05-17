using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAlgorithmConfigsTable : Migration
    {
        // Bu 27 key SystemConfigs tablosundan yeni SchedulingAlgorithmConfigs tablosuna
        // taşınıyor. Domain ayrımı için: otomatik programlama algoritma ayarları, genel
        // sistem ayarlarından (Attendance, GPA, Security, vs.) ayrı bir tabloda dursun.
        private const string AlgorithmKeysSqlList = """
            (
                'Classroom.AmphiThreshold',
                'Classroom.OccupancyTarget.Min',
                'Classroom.OccupancyTarget.Max',
                'SoftConstraint.S2.MaxDailyHours',
                'SoftConstraint.S3.MaxSectionsPerSlot',
                'SoftConstraint.S4.MinCoPairThreshold',
                'SoftConstraint.S7.MaxGapSlots',
                'Schedule.LunchBreak.IsHard',
                'Schedule.MaxConsecutiveHours',
                'Scheduling.MaxBacktrackingDepth',
                'Schedule.TheoryLabSplitThreshold',
                'Schedule.SplittableBlockTypes',
                'Schedule.SA.MaxIterations',
                'Schedule.SA.InitialTemperature',
                'Schedule.SA.TimeoutSeconds',
                'Schedule.SA.StagnationLimit',
                'Schedule.SA.BacktrackLimit',
                'Schedule.SA.RelocateWeight',
                'Schedule.SA.SwapWeight',
                'Schedule.SA.ChainWeight',
                'Schedule.LNS.MaxIterations',
                'Schedule.LNS.TimeoutSeconds',
                'Schedule.LNS.DestroySize',
                'Schedule.MRV.ClassroomWeight',
                'Schedule.MRV.SlotWeight',
                'Schedule.MRV.LabWeight',
                'Schedule.MRV.CapacityWeight'
            )
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingAlgorithmConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAlgorithmConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAlgorithmConfigs_Key",
                table: "SchedulingAlgorithmConfigs",
                column: "Key",
                unique: true);

            // SystemConfigs'tan algoritma key'lerini yeni tabloya kopyala (Category yeniden eşlenir).
            migrationBuilder.Sql($"""
                INSERT INTO "SchedulingAlgorithmConfigs" ("Key", "Value", "Description", "Category", "DataType")
                SELECT "Key", "Value", "Description",
                    CASE
                        WHEN "Key" LIKE 'Schedule.SA.%' THEN 'SA'
                        WHEN "Key" LIKE 'Schedule.LNS.%' THEN 'LNS'
                        WHEN "Key" LIKE 'Schedule.MRV.%' THEN 'MRV'
                        WHEN "Key" IN ('Classroom.AmphiThreshold', 'Schedule.LunchBreak.IsHard', 'Schedule.MaxConsecutiveHours') THEN 'HardThreshold'
                        WHEN "Key" IN ('Classroom.OccupancyTarget.Min', 'Classroom.OccupancyTarget.Max',
                                       'SoftConstraint.S2.MaxDailyHours', 'SoftConstraint.S3.MaxSectionsPerSlot',
                                       'SoftConstraint.S4.MinCoPairThreshold', 'SoftConstraint.S7.MaxGapSlots') THEN 'SoftThreshold'
                        ELSE 'Other'
                    END AS "Category",
                    "DataType"
                FROM "SystemConfigs"
                WHERE "Key" IN {AlgorithmKeysSqlList};

                DELETE FROM "SystemConfigs" WHERE "Key" IN {AlgorithmKeysSqlList};
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: SchedulingAlgorithmConfigs satırlarını SystemConfigs'a geri yaz,
            // sonra tabloyu drop et. Eski Category değerleri (Scheduling/Schedule) restore edilir.
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfigs" ("Key", "Value", "Description", "Category", "DataType")
                SELECT "Key", "Value", "Description",
                    CASE
                        WHEN "Key" IN ('Schedule.LunchBreak.IsHard', 'Schedule.MaxConsecutiveHours') THEN 'Schedule'
                        ELSE 'Scheduling'
                    END AS "Category",
                    "DataType"
                FROM "SchedulingAlgorithmConfigs"
                WHERE "Key" IN {AlgorithmKeysSqlList};
                """);

            migrationBuilder.DropTable(
                name: "SchedulingAlgorithmConfigs");
        }
    }
}
