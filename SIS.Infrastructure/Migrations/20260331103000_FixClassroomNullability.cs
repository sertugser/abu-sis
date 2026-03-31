using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260331103000_FixClassroomNullability")]
    public partial class FixClassroomNullability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Classrooms"
                SET "BuildingName" = ''
                WHERE "BuildingName" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Classrooms"
                SET "Floor" = 0
                WHERE "Floor" IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "BuildingName" SET DEFAULT '';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "BuildingName" SET NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "Floor" SET DEFAULT 0;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "Floor" SET NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "BuildingName" DROP NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "BuildingName" DROP DEFAULT;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "Floor" DROP NOT NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Classrooms"
                ALTER COLUMN "Floor" DROP DEFAULT;
                """);
        }
    }
}
