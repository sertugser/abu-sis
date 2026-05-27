using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionDelegationOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Çakışan aktif vekâlet'i DB seviyesinde engelle (uygulama-içi kontrolün yarış koşuluna karşı).
            // Aynı (Yetkiyi Veren, Vekil) çifti için, iptal edilmemiş kayıtlarda zaman aralıkları çakışamaz.
            // btree_gist: integer eşitliği (=) ile zaman aralığı çakışmasını (&&) tek GIST index'inde birleştirir.
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(@"
                ALTER TABLE ""PermissionDelegations""
                ADD CONSTRAINT ""EX_PermissionDelegations_NoOverlap""
                EXCLUDE USING gist (
                    ""DelegatorUserId"" WITH =,
                    ""DelegateeUserId"" WITH =,
                    tstzrange(""StartsAtUtc"", ""EndsAtUtc"") WITH &&
                ) WHERE (""RevokedAtUtc"" IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""PermissionDelegations"" DROP CONSTRAINT IF EXISTS ""EX_PermissionDelegations_NoOverlap"";");
        }
    }
}
