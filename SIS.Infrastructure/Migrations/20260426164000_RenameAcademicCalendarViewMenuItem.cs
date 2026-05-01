using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426164000_RenameAcademicCalendarViewMenuItem")]
    public partial class RenameAcademicCalendarViewMenuItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MenuItems"
                SET "Label" = 'Akademik Takvim',
                    "UpdatedAt" = NOW()
                WHERE "Route" = '/akademik-takvim'
                  AND "Slug" = 'akademik-takvim-goruntuleme';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MenuItems"
                SET "Label" = 'Akademik Takvim Görüntüleme',
                    "UpdatedAt" = NOW()
                WHERE "Route" = '/akademik-takvim'
                  AND "Slug" = 'akademik-takvim-goruntuleme';
                """);
        }
    }
}
