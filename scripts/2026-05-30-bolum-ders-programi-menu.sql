-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-30: "Bölüm Ders Programı" sidebar item + Student rolüne yetki
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: PermissionSeed.cs, MenuStructureSeed.cs ve
-- RolePermissionSeed.cs idempotent — tablo doluysa return ediyor. Kod
-- değişiklikleri (yeni Permission key'leri, MenuItem, Role-Permission
-- satırları) mevcut DB'ye yansımaz; sadece yeni kurulumlarda geçerli.
--
-- DEĞİŞİKLİKLER:
--   1) Permissions: menu.bolum-ders-programi.view + .export
--   2) MenuItems: "Bölüm Ders Programı" → /bolum-ders-programi
--      (slug=bolum-ders-programi). "Genel İşlemler" modülünde
--      SortOrder=6 ("Açılan Bölüm Dersleri"nin hemen altı).
--   3) MenuItemPermissions: view permission'ı item'a bağla
--   4) RolePermissions: Student rolüne hem .view hem .export
--
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. Permission'ları idempotent ekle ─────────────────────────────────
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES
  ('menu.bolum-ders-programi.view',   1, 'Bölüm ders programı (program × sınıf haftalık görünüm) görüntüleme', NOW()),
  ('menu.bolum-ders-programi.export', 1, 'Bölüm ders programı PDF indirme', NOW())
ON CONFLICT ("Key") DO NOTHING;

-- ── 2. Ön kontrol: target modül mevcut mu? ─────────────────────────────
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "MenuModules" WHERE "Slug" = 'genel-islemler') THEN
    RAISE EXCEPTION 'genel-islemler menü modülü bulunamadı.';
  END IF;
END $$;

-- ── 3. MenuItem: Bölüm Ders Programı ───────────────────────────────────
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
SELECT mm."Id", 'Bölüm Ders Programı', '/bolum-ders-programi', 'bolum-ders-programi', 6, false, true, NOW(), NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'genel-islemler'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItems" mi
    WHERE mi."Slug" = 'bolum-ders-programi'
  );

-- ── 4. MenuItem ↔ Permission bağı ──────────────────────────────────────
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'bolum-ders-programi'
  AND p."Key" = 'menu.bolum-ders-programi.view'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = mi."Id" AND mip."PermissionId" = p."Id"
  );

-- ── 5. Student rolüne view + export ata ────────────────────────────────
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'Student'
  AND p."Key" IN ('menu.bolum-ders-programi.view', 'menu.bolum-ders-programi.export')
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── Doğrulama ──────────────────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug", mi."Route"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'genel-islemler'
ORDER BY mi."SortOrder";

COMMIT;
