using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 11 Faz 4.7 — RuleEngine path'i default aktive eder. 22 unit + integration test ile
    /// doğrulandı (V1V2DriftTests, RuleEngineIntegrationTests, H17_FaceToFaceSlotRuleTests).
    /// V1 evaluator path hâlâ DI'da, flag false'a çekilince rollback'lik geri aktive olur.
    /// Faz 5'te V1 silinince hem bu flag hem migration zinciri (AddUseV2RuntimeRulesFlag +
    /// EnableV2RuntimeRulesByDefault + RemoveUseV2RuntimeRulesFlag) tek seferde temizlenir.
    /// </summary>
    public partial class EnableV2RuntimeRulesByDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""SystemConfigs""
SET ""Value"" = 'true'
WHERE ""Key"" = 'Schedule.UseV2RuntimeRules';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""SystemConfigs""
SET ""Value"" = 'false'
WHERE ""Key"" = 'Schedule.UseV2RuntimeRules';
");
        }
    }
}
