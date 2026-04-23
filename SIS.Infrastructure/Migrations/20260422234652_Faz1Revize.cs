using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Faz1Revize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Eski (Year, Semester) unique index'i düşür — yeni (Year, TermType) gelecek
            migrationBuilder.DropIndex(
                name: "IX_AcademicTerms_Year_Semester",
                table: "AcademicTerms");

            // 2. AcademicTerms.Status → YoksisStatus olarak yeniden adlandır
            //    (Eski 5-değerli TermStatus sütunu artık YoksisStatus olacak)
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AcademicTerms",
                newName: "YoksisStatus");

            // 3. YoksisStatus değerlerini düzelt: eski TermStatus (0-4) → YoksisStatus (0=Inactive, 1=Active)
            //    Ready(2) → Active(1), diğerleri (Draft/Preparing/Closed/Archived) → Inactive(0)
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms"" SET ""YoksisStatus"" = CASE
                    WHEN ""YoksisStatus"" = 2 THEN 1
                    ELSE 0
                END;");

            // 4. TermType değerlerini eski (Semester sütunu silinmeden önce!) yeni 9-değerli şemaya mapple
            //    Eski TermType 1-tabanlı: Regular=1, Summer=2, Intensive=3, Exemption=4
            //    Eski Semester 1-tabanlı: Fall=1, Spring=2, Summer=3
            //    Regular(1)+Fall(1)   → Fall(0)
            //    Regular(1)+Spring(2) → Spring(1)
            //    Regular(1)+Summer(3) → Summer(2)
            //    Summer(2)+any        → Summer(2)
            //    Intensive(3)+any     → SummerResit(5)
            //    Exemption(4)+any     → Exemption(8)
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms"" SET ""TermType"" = CASE
                    WHEN ""TermType"" = 1 AND ""Semester"" = 1 THEN 0
                    WHEN ""TermType"" = 1 AND ""Semester"" = 2 THEN 1
                    WHEN ""TermType"" = 1 AND ""Semester"" = 3 THEN 2
                    WHEN ""TermType"" = 2 THEN 2
                    WHEN ""TermType"" = 3 THEN 5
                    WHEN ""TermType"" = 4 THEN 8
                    ELSE 0
                END;");

            // 5. Artık ihtiyaç kalmayan Semester sütununu düşür
            migrationBuilder.DropColumn(
                name: "Semester",
                table: "AcademicTerms");

            // 6. CourseSections.Status (enum int) ekle — IsActive düşürülmeden önce
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CourseSections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 7. IsActive → Status veri göçü (Status eklendikten, IsActive silinmeden önce)
            //    IsActive=true  → Active(1)
            //    IsActive=false → Cancelled(2)
            migrationBuilder.Sql(@"
                UPDATE ""CourseSections"" SET ""Status"" = CASE
                    WHEN ""IsActive"" = true THEN 1
                    ELSE 2
                END;");

            // 8. Artık ihtiyaç kalmayan IsActive sütununu düşür
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CourseSections");

            // 9. AcademicTerms: yeni sütunlar ekle
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "AcademicTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OtherName",
                table: "AcademicTerms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortEnglishName",
                table: "AcademicTerms",
                type: "text",
                nullable: false,
                defaultValue: "");

            // 10. Yeni (Year, TermType) unique index oluştur
            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_Year_TermType",
                table: "AcademicTerms",
                columns: new[] { "Year", "TermType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicTerms_Year_TermType",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "OtherName",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "ShortEnglishName",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CourseSections");

            migrationBuilder.RenameColumn(
                name: "YoksisStatus",
                table: "AcademicTerms",
                newName: "Status");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CourseSections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Semester",
                table: "AcademicTerms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_Year_Semester",
                table: "AcademicTerms",
                columns: new[] { "Year", "Semester" },
                unique: true);
        }
    }
}
