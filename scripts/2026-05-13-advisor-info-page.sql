-- ──────────────────────────────────────────────────────────────────────
-- Danışman Bilgileri Sayfası — Permission + Menu seed (canlı DB)
-- Plan: docs/superpowers/plans/2026-05-13-advisor-info-page.md
-- Spec: docs/superpowers/specs/2026-05-13-advisor-info-page-design.md
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs idempotent (boş tablo guard).
-- PermissionSeed.cs merge-based (restart'ta otomatik DB'ye düşer) AMA
-- RolePermission ve MenuItem satırları manuel SQL gerektirir.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. Permission INSERT (idempotent) ──────────────────────────────────
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt", "UpdatedAt")
VALUES ('menu.danisman-bilgileri.view', 1, 'Danışman bilgileri sayfası (öğrenci görünümü)', NOW(), NOW())
ON CONFLICT ("Key") DO NOTHING;

-- ── 2. Student rolüne RolePermission ekle ──────────────────────────────
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "CreatedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'Student'
  AND p."Key" = 'menu.danisman-bilgileri.view'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── 3. Alınan Dersler SortOrder 3 → 4 ──────────────────────────────────
UPDATE "MenuItems"
SET "SortOrder" = 4, "UpdatedAt" = NOW()
WHERE "Slug" = 'alinan-dersler'
  AND "ModuleId" = (SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'genel-islemler');

-- ── 4. Danışman Bilgileri MenuItem INSERT (idempotent) ────────────────
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
SELECT mm."Id", 'Danışman Bilgileri', '/danisman-bilgileri', 'danisman-bilgileri', 3, false, true, NOW(), NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'genel-islemler'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItems" mi
    WHERE mi."Slug" = 'danisman-bilgileri'
  );

-- ── 5. MenuItem'a permission bağla ─────────────────────────────────────
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'danisman-bilgileri'
  AND p."Key" = 'menu.danisman-bilgileri.view'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = mi."Id" AND mip."PermissionId" = p."Id"
  );

-- ── Doğrulama sorgusu ──────────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug", mi."Route"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'genel-islemler'
ORDER BY mi."SortOrder";

COMMIT;
