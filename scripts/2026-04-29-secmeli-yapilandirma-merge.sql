-- ──────────────────────────────────────────────────────────────────────
-- Seçmeli Yapılandırma — Yeni Birleşik Sayfa Sidebar Güncellemesi
-- Plan: docs/superpowers/plans/2026-04-29-secmeli-yapilandirma-merge.md
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs, PermissionSeed.cs ve
-- RolePermissionSeed.cs idempotent — tablolar boş değilse return ediyor.
-- C# seed değişiklikleri mevcut dev/prod DB'lere yansımaz.
--
-- BU SCRIPT'İN YAPTIĞI (7 adım):
--   1. Yeni Permission ekle: menu.secmeli-yapilandirma.view
--   2. 3 rol için RolePermission satırları ekle (SysAdmin, Dean, DeptManager)
--   3. Eski 2 MenuItem'ın MenuItemPermission bağlantılarını sil
--      (secmeli-ders-gruplari, secmeli-tipleri)
--   4. ders-islemleri modülünde SortOrder >= 8 olan item'ları 1 azalt
--      (silme ile açılan boşluğu kapat — EXISTS guard ile idempotent)
--   5. Eski 2 MenuItem'ı sil
--   6. Yeni MenuItem 'secmeli-yapilandirma' ekle (SortOrder=4)
--   7. Yeni MenuItem'ı yeni Permission'a bağla
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. Yeni Permission ekle ──────────────────────────────────────────
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES (
    'menu.secmeli-yapilandirma.view',
    0,
    'Seçmeli ders yapılandırma sayfası (Gruplar + Tipler tab wrapper)',
    NOW()
)
ON CONFLICT ("Key") DO NOTHING;

-- ── 2. 3 rol için RolePermission satırları ekle ──────────────────────
WITH new_perm AS (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" = 'menu.secmeli-yapilandirma.view'
)
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", np."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN new_perm np
WHERE r."Name" IN ('SystemAdministrator', 'Dean', 'DepartmentManager')
ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

-- ── 3. Eski 2 MenuItem'ın MenuItemPermission bağlantılarını sil ──────
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
    SELECT "Id" FROM "MenuItems"
    WHERE "Slug" IN ('secmeli-ders-gruplari', 'secmeli-tipleri')
);

-- ── 4. SortOrder >= 8 olan item'ları 1 azalt (boşluğu kapat) ─────────
--    EXISTS guard: eski item'lar hâlâ varsa (ilk çalışma) → UPDATE çalışır.
--    Eski item'lar silinmişse (yeniden çalışma) → UPDATE atlanır.
UPDATE "MenuItems"
SET "SortOrder" = "SortOrder" - 1,
    "UpdatedAt" = NOW()
WHERE "ModuleId" = (
    SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'ders-islemleri'
)
  AND "SortOrder" >= 8
  AND EXISTS (
      SELECT 1 FROM "MenuItems"
      WHERE "Slug" IN ('secmeli-ders-gruplari', 'secmeli-tipleri')
  );

-- ── 5. Eski 2 MenuItem'ı sil ─────────────────────────────────────────
DELETE FROM "MenuItems"
WHERE "Slug" IN ('secmeli-ders-gruplari', 'secmeli-tipleri');

-- ── 6. Yeni MenuItem ekle (SortOrder=4) ──────────────────────────────
INSERT INTO "MenuItems" (
    "ModuleId", "Label", "Route", "Slug", "SortOrder",
    "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt"
)
SELECT
    mm."Id",
    'Seçmeli Ders Yapılandırması',
    '/secmeli-yapilandirma',
    'secmeli-yapilandirma',
    4,
    false,
    true,
    NOW(),
    NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'ders-islemleri'
ON CONFLICT ("ModuleId", "Route") DO NOTHING;

-- ── 7. Yeni MenuItem'ı yeni Permission'a bağla ───────────────────────
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'secmeli-yapilandirma'
  AND p."Key"  = 'menu.secmeli-yapilandirma.view'
ON CONFLICT ("MenuItemId", "PermissionId") DO NOTHING;

-- ── Doğrulama sorguları ───────────────────────────────────────────────

-- ders-islemleri modülündeki tüm item'lar (sıralı)
SELECT mi."SortOrder", mi."Label", mi."Slug"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-islemleri'
ORDER BY mi."SortOrder";

-- menu.secmeli-* permission'ları ve kaç role verildiği
SELECT p."Key", p."Id", COUNT(rp."RoleId") AS "RoleSayisi"
FROM "Permissions" p
LEFT JOIN "RolePermissions" rp ON rp."PermissionId" = p."Id"
WHERE p."Key" LIKE 'menu.secmeli-%'
GROUP BY p."Key", p."Id"
ORDER BY p."Key";

COMMIT;
