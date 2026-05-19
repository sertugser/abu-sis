using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 11 Faz 0.4 — Runtime hard-rule validation V2 encoder mantığı (RuleEngine) üzerinden
    /// yapılır feature flag. Default false; Faz 4.7'de SystemConfig'ten true'ya çekilir; Faz 5
    /// V1 evaluator silindikten sonra ayrı migration ile key kaldırılır.
    /// </summary>
    public partial class AddUseV2RuntimeRulesFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
VALUES (
    'Schedule.UseV2RuntimeRules',
    'false',
    'Plan 11 — Runtime hard-rule validation V2 encoder mantığı (RuleEngine) üzerinden yapılır. False (default) iken eski V1 evaluator path''i çalışır. Faz 4 sonu true yapılır; Faz 5 V1 silindikten sonra config kaldırılır.',
    'Schedule',
    'bool'
)
ON CONFLICT (""Key"") DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SystemConfigs"" WHERE ""Key"" = 'Schedule.UseV2RuntimeRules';");
        }
    }
}
