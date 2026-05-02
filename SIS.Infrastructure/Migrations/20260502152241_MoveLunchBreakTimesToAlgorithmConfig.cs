using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveLunchBreakTimesToAlgorithmConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schedule.LunchBreakStart / Schedule.LunchBreakEnd hem TimeSlot grid seed'i
            // hem de algoritma factory tarafından okunuyor; tek otorite olsun diye
            // SchedulingAlgorithmConfigs'a taşınıyor. SeedData.TimeSlots.cs artık bu iki
            // key'i yeni tablodan okuyor.
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingAlgorithmConfigs" ("Key", "Value", "Description", "Category", "DataType")
                SELECT "Key", "Value", "Description", 'HardThreshold', "DataType"
                FROM "SystemConfigs"
                WHERE "Key" IN ('Schedule.LunchBreakStart', 'Schedule.LunchBreakEnd');

                DELETE FROM "SystemConfigs"
                WHERE "Key" IN ('Schedule.LunchBreakStart', 'Schedule.LunchBreakEnd');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "SystemConfigs" ("Key", "Value", "Description", "Category", "DataType")
                SELECT "Key", "Value", "Description", 'Schedule', "DataType"
                FROM "SchedulingAlgorithmConfigs"
                WHERE "Key" IN ('Schedule.LunchBreakStart', 'Schedule.LunchBreakEnd');

                DELETE FROM "SchedulingAlgorithmConfigs"
                WHERE "Key" IN ('Schedule.LunchBreakStart', 'Schedule.LunchBreakEnd');
                """);
        }
    }
}
