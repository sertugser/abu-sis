using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260429074000_AddAttendanceMenuItem")]
    public partial class AddAttendanceMenuItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    attendance_permission_id integer;
                    teaching_module_id integer;
                    attendance_menu_item_id integer;
                BEGIN
                    INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
                    VALUES ('menu.yoklama-girisi.view', 0, 'Yoklama girişi sayfası ve QR yoklama yönetimi', NOW())
                    ON CONFLICT ("Key") DO UPDATE
                    SET "Description" = EXCLUDED."Description";

                    SELECT "Id"
                    INTO attendance_permission_id
                    FROM "Permissions"
                    WHERE "Key" = 'menu.yoklama-girisi.view';

                    SELECT "Id"
                    INTO teaching_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'ogretim-islemleri';

                    IF teaching_module_id IS NOT NULL THEN
                        SELECT "Id"
                        INTO attendance_menu_item_id
                        FROM "MenuItems"
                        WHERE "ModuleId" = teaching_module_id
                          AND "Slug" = 'yoklama-girisi';

                        IF attendance_menu_item_id IS NULL THEN
                            UPDATE "MenuItems"
                            SET "SortOrder" = "SortOrder" + 1,
                                "UpdatedAt" = NOW()
                            WHERE "ModuleId" = teaching_module_id
                              AND "SortOrder" >= 3;

                            INSERT INTO "MenuItems" (
                                "ModuleId",
                                "Label",
                                "Route",
                                "Slug",
                                "SortOrder",
                                "OpenInNewTab",
                                "IsActive",
                                "CreatedAt",
                                "UpdatedAt")
                            VALUES (
                                teaching_module_id,
                                'Yoklama Girişi',
                                '/yoklama-girisi',
                                'yoklama-girisi',
                                3,
                                FALSE,
                                TRUE,
                                NOW(),
                                NOW())
                            RETURNING "Id" INTO attendance_menu_item_id;
                        ELSE
                            UPDATE "MenuItems"
                            SET "Label" = 'Yoklama Girişi',
                                "Route" = '/yoklama-girisi',
                                "SortOrder" = 3,
                                "IsActive" = TRUE,
                                "UpdatedAt" = NOW()
                            WHERE "Id" = attendance_menu_item_id;
                        END IF;

                        INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
                        SELECT attendance_menu_item_id, attendance_permission_id
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM "MenuItemPermissions"
                            WHERE "MenuItemId" = attendance_menu_item_id
                              AND "PermissionId" = attendance_permission_id
                        );
                    END IF;

                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt", "GrantedByUserId")
                    SELECT role."Id", attendance_permission_id, NOW(), NULL
                    FROM "AspNetRoles" role
                    WHERE role."Name" IN ('Instructor', 'AcademicAdvisor', 'DepartmentManager', 'Dean')
                      AND NOT EXISTS (
                          SELECT 1
                          FROM "RolePermissions" existing
                          WHERE existing."RoleId" = role."Id"
                            AND existing."PermissionId" = attendance_permission_id
                      );
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    attendance_permission_id integer;
                    teaching_module_id integer;
                    attendance_menu_item_id integer;
                BEGIN
                    SELECT "Id"
                    INTO attendance_permission_id
                    FROM "Permissions"
                    WHERE "Key" = 'menu.yoklama-girisi.view';

                    SELECT "Id"
                    INTO teaching_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'ogretim-islemleri';

                    SELECT "Id"
                    INTO attendance_menu_item_id
                    FROM "MenuItems"
                    WHERE "Slug" = 'yoklama-girisi'
                      AND "Route" = '/yoklama-girisi';

                    IF attendance_menu_item_id IS NOT NULL THEN
                        DELETE FROM "MenuItemPermissions"
                        WHERE "MenuItemId" = attendance_menu_item_id;

                        DELETE FROM "MenuItems"
                        WHERE "Id" = attendance_menu_item_id;
                    END IF;

                    IF teaching_module_id IS NOT NULL THEN
                        UPDATE "MenuItems"
                        SET "SortOrder" = "SortOrder" - 1,
                            "UpdatedAt" = NOW()
                        WHERE "ModuleId" = teaching_module_id
                          AND "SortOrder" > 3;
                    END IF;

                    IF attendance_permission_id IS NOT NULL THEN
                        DELETE FROM "RolePermissions"
                        WHERE "PermissionId" = attendance_permission_id;

                        DELETE FROM "Permissions"
                        WHERE "Id" = attendance_permission_id;
                    END IF;
                END $$;
                """);
        }
    }
}
