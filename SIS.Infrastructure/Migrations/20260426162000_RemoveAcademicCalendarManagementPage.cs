using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426162000_RemoveAcademicCalendarManagementPage")]
    public partial class RemoveAcademicCalendarManagementPage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    academic_module_id integer;
                    removed_sort_order integer;
                    management_permission_keys text[] := ARRAY[
                        'menu.akademik-takvim-yonetimi.view',
                        'menu.akademik-takvim-yonetimi.edit',
                        'menu.akademik-takvim-yonetimi.override'
                    ];
                BEGIN
                    SELECT "Id"
                    INTO academic_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'akademik-islemler'
                    LIMIT 1;

                    IF academic_module_id IS NOT NULL THEN
                        SELECT "SortOrder"
                        INTO removed_sort_order
                        FROM "MenuItems"
                        WHERE "ModuleId" = academic_module_id
                          AND ("Slug" = 'akademik-takvim-yonetimi' OR "Route" = '/akademik-takvim-yonetimi')
                        ORDER BY "SortOrder"
                        LIMIT 1;

                        DELETE FROM "MenuItemPermissions"
                        WHERE "MenuItemId" IN (
                            SELECT "Id"
                            FROM "MenuItems"
                            WHERE "ModuleId" = academic_module_id
                              AND ("Slug" = 'akademik-takvim-yonetimi' OR "Route" = '/akademik-takvim-yonetimi')
                        );

                        DELETE FROM "MenuItems"
                        WHERE "ModuleId" = academic_module_id
                          AND ("Slug" = 'akademik-takvim-yonetimi' OR "Route" = '/akademik-takvim-yonetimi');

                        IF removed_sort_order IS NOT NULL THEN
                            UPDATE "MenuItems"
                            SET "SortOrder" = "SortOrder" - 1,
                                "UpdatedAt" = NOW()
                            WHERE "ModuleId" = academic_module_id
                              AND "SortOrder" > removed_sort_order;
                        END IF;
                    END IF;

                    DELETE FROM "RolePermissions"
                    WHERE "PermissionId" IN (
                        SELECT "Id"
                        FROM "Permissions"
                        WHERE "Key" = ANY (management_permission_keys)
                    );

                    DELETE FROM "MenuItemPermissions"
                    WHERE "PermissionId" IN (
                        SELECT "Id"
                        FROM "Permissions"
                        WHERE "Key" = ANY (management_permission_keys)
                    );

                    DELETE FROM "Permissions"
                    WHERE "Key" = ANY (management_permission_keys);
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    academic_module_id integer;
                    view_permission_id integer;
                    management_item_id integer;
                BEGIN
                    INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
                    SELECT permission_key, 0, description, NOW()
                    FROM (VALUES
                        ('menu.akademik-takvim-yonetimi.view', 'Akademik takvim yönetim sayfası görüntüleme'),
                        ('menu.akademik-takvim-yonetimi.edit', 'Akademik takvim güncelleme (upsert)'),
                        ('menu.akademik-takvim-yonetimi.override', 'Akademik takvim faz override')
                    ) AS permission_seed(permission_key, description)
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "Permissions"
                        WHERE "Key" = permission_seed.permission_key
                    );

                    SELECT "Id"
                    INTO academic_module_id
                    FROM "MenuModules"
                    WHERE "Slug" = 'akademik-islemler'
                    LIMIT 1;

                    SELECT "Id"
                    INTO view_permission_id
                    FROM "Permissions"
                    WHERE "Key" = 'menu.akademik-takvim-yonetimi.view'
                    LIMIT 1;

                    IF academic_module_id IS NOT NULL AND view_permission_id IS NOT NULL THEN
                        SELECT "Id"
                        INTO management_item_id
                        FROM "MenuItems"
                        WHERE "ModuleId" = academic_module_id
                          AND ("Slug" = 'akademik-takvim-yonetimi' OR "Route" = '/akademik-takvim-yonetimi')
                        LIMIT 1;

                        IF management_item_id IS NULL THEN
                            UPDATE "MenuItems"
                            SET "SortOrder" = "SortOrder" + 1,
                                "UpdatedAt" = NOW()
                            WHERE "ModuleId" = academic_module_id
                              AND "SortOrder" >= 6;

                            INSERT INTO "MenuItems"
                                ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
                            VALUES
                                (academic_module_id, 'Akademik Takvim Yönetimi', '/akademik-takvim-yonetimi', 'akademik-takvim-yonetimi', 6, FALSE, TRUE, NOW(), NOW())
                            RETURNING "Id" INTO management_item_id;
                        END IF;

                        INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
                        SELECT management_item_id, view_permission_id
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM "MenuItemPermissions"
                            WHERE "MenuItemId" = management_item_id
                              AND "PermissionId" = view_permission_id
                        );

                        INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt", "GrantedByUserId")
                        SELECT roles."Id", view_permission_id, NOW(), NULL
                        FROM "AspNetRoles" roles
                        WHERE roles."Name" = 'AdministrativeStaff'
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "RolePermissions" existing
                              WHERE existing."RoleId" = roles."Id"
                                AND existing."PermissionId" = view_permission_id
                          );
                    END IF;
                END $$;
                """);
        }
    }
}
