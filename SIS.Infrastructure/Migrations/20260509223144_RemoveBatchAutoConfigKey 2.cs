using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 2026-05-10: BatchAuto.cs ölü kod silinmesi takip migration'ı. SchedulingService.BatchAuto.cs
    /// + 3 BatchAuto* DTO + SchedulingController.BatchAutoSchedule endpoint + frontend
    /// useBatchAutoSchedule hook + scheduling.api.ts batchAutoSchedule fonksiyonu silindiği için
    /// `Scheduling.MaxBacktrackingDepth` config key'i artık atıl. SystemConfigs'ten temizlenir.
    /// </summary>
    public partial class RemoveBatchAutoConfigKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""SystemConfigs""
                WHERE ""Key"" = 'Scheduling.MaxBacktrackingDepth';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
                VALUES (
                    'Scheduling.MaxBacktrackingDepth',
                    '3',
                    '(V1 BatchAuto atıl) Backtracking derinliği. Migration BatchAuto silinmesi sonrası ekleniyor.',
                    'Other',
                    'int'
                );
            ");
        }
    }
}
