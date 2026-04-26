using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426154500_RestoreAcademicCalendarViewMenuItem")]
    public partial class RestoreAcademicCalendarViewMenuItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    academic_module_id integer;
                    calendar_permission_id integer;
                    calendar_item_id integer;
                BEGIN
                    SELECT "Id"
                    INTO academic_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'akademik-islemler'
                    LIMIT 1;

                    SELECT "Id"
                    INTO calendar_permission_id
                    FROM "Permissions"
                    WHERE "Key" = 'menu.akademik-takvim.view'
                    LIMIT 1;

                    IF academic_module_id IS NOT NULL AND calendar_permission_id IS NOT NULL THEN
                        SELECT "Id"
                        INTO calendar_item_id
                        FROM "MenuItems"
                        WHERE "ModuleId" = academic_module_id
                          AND ("Slug" = 'akademik-takvim-goruntuleme' OR "Route" = '/akademik-takvim')
                        LIMIT 1;

                        IF calendar_item_id IS NULL THEN
                            UPDATE "MenuItems"
                            SET "SortOrder" = "SortOrder" + 1,
                                "UpdatedAt" = NOW()
                            WHERE "ModuleId" = academic_module_id
                              AND "SortOrder" >= 5;

                            INSERT INTO "MenuItems"
                                ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
                            VALUES
                                (academic_module_id, 'Akademik Takvim Görüntüleme', '/akademik-takvim', 'akademik-takvim-goruntuleme', 5, FALSE, TRUE, NOW(), NOW())
                            RETURNING "Id" INTO calendar_item_id;
                        ELSE
                            UPDATE "MenuItems"
                            SET "Label" = 'Akademik Takvim Görüntüleme',
                                "Route" = '/akademik-takvim',
                                "Slug" = 'akademik-takvim-goruntuleme',
                                "SortOrder" = 5,
                                "IsActive" = TRUE,
                                "UpdatedAt" = NOW()
                            WHERE "Id" = calendar_item_id;
                        END IF;

                        INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
                        SELECT calendar_item_id, calendar_permission_id
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM "MenuItemPermissions"
                            WHERE "MenuItemId" = calendar_item_id
                              AND "PermissionId" = calendar_permission_id
                        );
                    END IF;

                    IF calendar_permission_id IS NOT NULL THEN
                        INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt", "GrantedByUserId")
                        SELECT roles."Id", calendar_permission_id, NOW(), NULL
                        FROM "AspNetRoles" roles
                        WHERE roles."Name" IN ('AdministrativeStaff', 'DepartmentManager', 'Dean')
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "RolePermissions" existing
                              WHERE existing."RoleId" = roles."Id"
                                AND existing."PermissionId" = calendar_permission_id
                          );
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    academic_module_id integer;
                    calendar_permission_id integer;
                    calendar_item_id integer;
                BEGIN
                    SELECT "Id"
                    INTO academic_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'akademik-islemler'
                    LIMIT 1;

                    SELECT "Id"
                    INTO calendar_permission_id
                    FROM "Permissions"
                    WHERE "Key" = 'menu.akademik-takvim.view'
                    LIMIT 1;

                    IF academic_module_id IS NOT NULL THEN
                        SELECT "Id"
                        INTO calendar_item_id
                        FROM "MenuItems"
                        WHERE "ModuleId" = academic_module_id
                          AND "Slug" = 'akademik-takvim-goruntuleme'
                        LIMIT 1;

                        IF calendar_item_id IS NOT NULL THEN
                            DELETE FROM "MenuItemPermissions"
                            WHERE "MenuItemId" = calendar_item_id;

                            DELETE FROM "MenuItems"
                            WHERE "Id" = calendar_item_id;

                            UPDATE "MenuItems"
                            SET "SortOrder" = "SortOrder" - 1,
                                "UpdatedAt" = NOW()
                            WHERE "ModuleId" = academic_module_id
                              AND "SortOrder" > 5;
                        END IF;
                    END IF;

                    IF calendar_permission_id IS NOT NULL THEN
                        DELETE FROM "RolePermissions" role_permissions
                        USING "AspNetRoles" roles
                        WHERE role_permissions."RoleId" = roles."Id"
                          AND role_permissions."PermissionId" = calendar_permission_id
                          AND roles."Name" IN ('AdministrativeStaff', 'DepartmentManager', 'Dean');
                    END IF;
                END $$;
                """);
        }
    }
}
