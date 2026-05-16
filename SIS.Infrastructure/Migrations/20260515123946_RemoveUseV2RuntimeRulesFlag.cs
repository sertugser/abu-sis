using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 11 Faz 5 — V1 hard evaluator silindi, RuleEngine her zaman aktif.
    /// <c>Schedule.UseV2RuntimeRules</c> flag'i artık gereksiz; SystemConfigs'ten kaldırılır.
    /// </summary>
    public partial class RemoveUseV2RuntimeRulesFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SystemConfigs"" WHERE ""Key"" = 'Schedule.UseV2RuntimeRules';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: flag'i yeniden ekle (default false).
            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'Schedule.UseV2RuntimeRules',
    'false',
    'Plan 11 rollback — RuleEngine kapatma flag''i.',
    'Schedule',
    'bool'
)
ON CONFLICT (""Key"") DO NOTHING;
");
        }
    }
}
