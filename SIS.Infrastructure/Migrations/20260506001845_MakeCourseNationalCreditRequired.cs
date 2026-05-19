using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCourseNationalCreditRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Mevcut NULL kayıtları Türk eğitim sistemi geleneksel formülüyle doldur:
            //    Ulusal Kredi = TheoryHours + LabHours / 2 (tam sayı bölme).
            //    Bu, AKTS'den bağımsız ama "Theory + Lab/2" konvensiyonuna uyar.
            //    Sıfır-saat dersler 0 alır (staj/proje gibi); admin sonradan revize edebilir.
            migrationBuilder.Sql(
                "UPDATE \"Courses\" " +
                "SET \"NationalCredit\" = \"TheoryHours\" + \"LabHours\" / 2 " +
                "WHERE \"NationalCredit\" IS NULL;");

            // 2) NOT NULL kısıtı ekle. Default 0 yedek emniyet (ileride NULL olamaz zaten).
            migrationBuilder.AlterColumn<int>(
                name: "NationalCredit",
                table: "Courses",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "NationalCredit",
                table: "Courses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
