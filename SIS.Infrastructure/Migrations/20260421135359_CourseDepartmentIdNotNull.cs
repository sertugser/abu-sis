using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CourseDepartmentIdNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Madde 10: Course.DepartmentId NOT NULL migration
            // 1) Sahipsiz (null DepartmentId) dersleri koordinatörlüğe taşı.
            //    Öncelik: "TBK" kodlu koordinatörlük → yoksa ilk department (fallback).
            migrationBuilder.Sql(@"
                UPDATE ""Courses""
                SET ""DepartmentId"" = COALESCE(
                    (SELECT ""Id"" FROM ""Departments"" WHERE ""Code"" = 'TBK' AND ""IsServiceUnit"" = TRUE LIMIT 1),
                    (SELECT ""Id"" FROM ""Departments"" ORDER BY ""Id"" LIMIT 1)
                )
                WHERE ""DepartmentId"" IS NULL;
            ");

            // 2) Sütunu NOT NULL yap.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Courses"" ALTER COLUMN ""DepartmentId"" SET NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Courses"" ALTER COLUMN ""DepartmentId"" DROP NOT NULL;
            ");
            // Veriyi geri almaya çalışmıyoruz — null course'lar genel kurum pratiği değil.
        }
    }
}
