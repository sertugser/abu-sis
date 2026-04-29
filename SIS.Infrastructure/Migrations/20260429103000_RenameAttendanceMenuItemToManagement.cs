using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260429103000_RenameAttendanceMenuItemToManagement")]
    public partial class RenameAttendanceMenuItemToManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MenuItems"
                SET "Label" = 'Yoklama Yönetimi',
                    "UpdatedAt" = NOW()
                WHERE "Slug" = 'yoklama-girisi'
                  AND "Route" = '/yoklama-girisi';

                UPDATE "Permissions"
                SET "Description" = 'Yoklama yönetimi sayfası, geçmiş yoklamalar ve QR yoklama yönetimi'
                WHERE "Key" = 'menu.yoklama-girisi.view';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MenuItems"
                SET "Label" = 'Yoklama Girişi',
                    "UpdatedAt" = NOW()
                WHERE "Slug" = 'yoklama-girisi'
                  AND "Route" = '/yoklama-girisi';

                UPDATE "Permissions"
                SET "Description" = 'Yoklama girişi sayfası ve QR yoklama yönetimi'
                WHERE "Key" = 'menu.yoklama-girisi.view';
                """);
        }
    }
}
