using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SingleActiveTermFlagModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActivated",
                table: "AcademicTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            // ── Tek-aktif veri normalizasyonu (tek seferlik) ───────────────────────────
            // Yeni model: aynı anda en çok bir dönem "Kullanıma Açık" (IsActivated=true).
            // Eski modelde IsActivated varsayılanı true olduğundan birden fazla dönem açık
            // kalmış olabilir. Burada tutarlı tek-aktif duruma çekiyoruz.

            // 1) İptal edilmiş ama hâlâ açık görünen dönemleri kapat.
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms""
                SET ""IsActivated"" = false
                WHERE ""IsCancelled"" = true AND ""IsActivated"" = true;");

            // 2) Birden fazla (iptal edilmemiş) açık dönem varsa, en yeni başlangıçlı olan
            //    hariç hepsini kapat (Banner: 'latest activated term' tiebreak'i).
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms""
                SET ""IsActivated"" = false
                WHERE ""IsActivated"" = true
                  AND ""Id"" <> (
                      SELECT ""Id"" FROM ""AcademicTerms""
                      WHERE ""IsActivated"" = true AND ""IsCancelled"" = false
                      ORDER BY ""TermStart"" DESC, ""Id"" DESC
                      LIMIT 1
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActivated",
                table: "AcademicTerms",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
