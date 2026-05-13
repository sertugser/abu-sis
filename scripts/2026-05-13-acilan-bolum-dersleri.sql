-- ──────────────────────────────────────────────────────────────────────
-- Açılan Bölüm Dersleri Sayfası — Permission + Menu seed (canlı DB)
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs idempotent (boş tablo guard).
-- PermissionSeed.cs merge-based (restart'ta otomatik düşer) AMA
-- RolePermission ve MenuItem satırları manuel SQL gerektirir.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. Permission INSERT (idempotent) ──────────────────────────────────
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES ('menu.acilan-bolum-dersleri.view', 1, 'Açılan bölüm dersleri sayfası (aktif dönem section listesi)', NOW())
ON CONFLICT ("Key") DO NOTHING;

-- ── 2. Student rolüne RolePermission ekle ──────────────────────────────
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'Student'
  AND p."Key" = 'menu.acilan-bolum-dersleri.view'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── 3. Açılan Bölüm Dersleri MenuItem INSERT (idempotent) ─────────────
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
SELECT mm."Id", 'Açılan Bölüm Dersleri', '/acilan-bolum-dersleri', 'acilan-bolum-dersleri', 5, false, true, NOW(), NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'genel-islemler'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItems" mi
    WHERE mi."Slug" = 'acilan-bolum-dersleri'
  );

-- ── 4. MenuItem'a permission bağla ─────────────────────────────────────
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'acilan-bolum-dersleri'
  AND p."Key" = 'menu.acilan-bolum-dersleri.view'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = mi."Id" AND mip."PermissionId" = p."Id"
  );

-- ── Doğrulama ──────────────────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug", mi."Route"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'genel-islemler'
ORDER BY mi."SortOrder";

COMMIT;
