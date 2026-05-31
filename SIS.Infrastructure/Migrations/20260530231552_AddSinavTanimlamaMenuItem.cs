using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSinavTanimlamaMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    view_perm_id integer;
                    manage_perm_id integer;
                    module_id integer;
                    item_id integer;
                    insert_order integer;
                BEGIN
                    -- 1) Permission'ları upsert et (Category 0 = Menu)
                    INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
                    VALUES ('menu.sinav-tanimlama.view', 0, 'Sınav Tanımlama sayfası (sınav oturumlarını görme)', NOW())
                    ON CONFLICT ("Key") DO UPDATE SET "Description" = EXCLUDED."Description";

                    INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
                    VALUES ('menu.sinav-tanimlama.manage', 0, 'Sınav oturumu ekle/düzenle/sil (tarih/derslik/ilan)', NOW())
                    ON CONFLICT ("Key") DO UPDATE SET "Description" = EXCLUDED."Description";

                    SELECT "Id" INTO view_perm_id FROM "Permissions" WHERE "Key" = 'menu.sinav-tanimlama.view';
                    SELECT "Id" INTO manage_perm_id FROM "Permissions" WHERE "Key" = 'menu.sinav-tanimlama.manage';

                    SELECT "Id" INTO module_id FROM "MenuModules" WHERE "Slug" = 'ogretim-islemleri';

                    IF module_id IS NOT NULL THEN
                        SELECT "Id" INTO item_id FROM "MenuItems"
                        WHERE "ModuleId" = module_id AND "Slug" = 'sinav-tanimlama';

                        IF item_id IS NULL THEN
                            -- Not Girişi'nin sırasını al; yoksa modülün sonuna ekle
                            SELECT "SortOrder" INTO insert_order FROM "MenuItems"
                            WHERE "ModuleId" = module_id AND "Slug" = 'not-girisi';

                            IF insert_order IS NULL THEN
                                SELECT COALESCE(MAX("SortOrder"), 0) + 1 INTO insert_order
                                FROM "MenuItems" WHERE "ModuleId" = module_id;
                            ELSE
                                -- Not Girişi ve sonrasını bir kaydır (Sınav Tanımlama önce gelsin)
                                UPDATE "MenuItems" SET "SortOrder" = "SortOrder" + 1, "UpdatedAt" = NOW()
                                WHERE "ModuleId" = module_id AND "SortOrder" >= insert_order;
                            END IF;

                            INSERT INTO "MenuItems" (
                                "ModuleId", "Label", "Route", "Slug", "SortOrder",
                                "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
                            VALUES (
                                module_id, 'Sınav Tanımlama', '/sinav-tanimlama', 'sinav-tanimlama', insert_order,
                                FALSE, TRUE, NOW(), NOW())
                            RETURNING "Id" INTO item_id;
                        END IF;

                        INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
                        SELECT item_id, view_perm_id
                        WHERE NOT EXISTS (
                            SELECT 1 FROM "MenuItemPermissions"
                            WHERE "MenuItemId" = item_id AND "PermissionId" = view_perm_id);
                    END IF;

                    -- 2) Rollere grant (view + manage) — idempotent
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt", "GrantedByUserId")
                    SELECT role."Id", perm_id, NOW(), NULL
                    FROM "AspNetRoles" role
                    CROSS JOIN (VALUES (view_perm_id), (manage_perm_id)) AS p(perm_id)
                    WHERE role."Name" IN ('Instructor', 'DepartmentManager', 'Dean')
                      AND NOT EXISTS (
                          SELECT 1 FROM "RolePermissions" existing
                          WHERE existing."RoleId" = role."Id" AND existing."PermissionId" = p.perm_id);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    view_perm_id integer;
                    manage_perm_id integer;
                    module_id integer;
                    item_id integer;
                    item_order integer;
                BEGIN
                    SELECT "Id" INTO view_perm_id FROM "Permissions" WHERE "Key" = 'menu.sinav-tanimlama.view';
                    SELECT "Id" INTO manage_perm_id FROM "Permissions" WHERE "Key" = 'menu.sinav-tanimlama.manage';
                    SELECT "Id" INTO module_id FROM "MenuModules" WHERE "Slug" = 'ogretim-islemleri';

                    SELECT "Id", "SortOrder" INTO item_id, item_order FROM "MenuItems"
                    WHERE "Slug" = 'sinav-tanimlama' AND "Route" = '/sinav-tanimlama';

                    IF item_id IS NOT NULL THEN
                        DELETE FROM "MenuItemPermissions" WHERE "MenuItemId" = item_id;
                        DELETE FROM "MenuItems" WHERE "Id" = item_id;
                        IF module_id IS NOT NULL THEN
                            UPDATE "MenuItems" SET "SortOrder" = "SortOrder" - 1, "UpdatedAt" = NOW()
                            WHERE "ModuleId" = module_id AND "SortOrder" > item_order;
                        END IF;
                    END IF;

                    DELETE FROM "RolePermissions" WHERE "PermissionId" IN (view_perm_id, manage_perm_id);
                    DELETE FROM "Permissions" WHERE "Id" IN (view_perm_id, manage_perm_id);
                END $$;
                """);
        }
    }
}
