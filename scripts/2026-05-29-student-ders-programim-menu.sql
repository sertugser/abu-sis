-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-29: Öğrenci "Ders Programım" sidebar item + Student rolüne izin
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs ve RolePermissionSeed.cs
-- idempotent — tablo doluysa return ediyor. Kod değişiklikleri (yeni
-- MenuItem ve yeni Role-Permission satırı) mevcut DB'ye yansımaz; sadece
-- yeni kurulumlarda geçerli.
--
-- DEĞİŞİKLİKLER:
--   1) MenuItems'a yeni satır: "Ders Programım" → /ders-programim
--      (slug=ders-programim-ogrenci; hoca grubundaki "ders-programim"
--      slug'ı ile çakışmaz). "Ders ve Dönem İşlemleri" modülünde
--      SortOrder=2 (Ders Kayıt'ın hemen altı).
--   2) MenuItemPermissions: yeni item'ı menu.ders-programim.view
--      permission'ına bağla (mevcut anahtar, hocada zaten kullanılıyor).
--   3) RolePermissions: Student rolü → menu.ders-programim.view.
--
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 0. Ön kontrol: permission ve hedef modül mevcut mu? ────────────────
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.ders-programim.view') THEN
    RAISE EXCEPTION 'menu.ders-programim.view permission bulunamadı.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "MenuModules" WHERE "Slug" = 'ders-ve-donem-islemleri') THEN
    RAISE EXCEPTION 'ders-ve-donem-islemleri menü modülü bulunamadı.';
  END IF;
END $$;

-- ── 1. MenuItem: Ders Programım (öğrenci grubu) ────────────────────────
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
SELECT mm."Id", 'Ders Programım', '/ders-programim', 'ders-programim-ogrenci', 2, false, true, NOW(), NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'ders-ve-donem-islemleri'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItems" mi
    WHERE mi."Slug" = 'ders-programim-ogrenci'
  );

-- ── 2. MenuItem ↔ Permission bağı ──────────────────────────────────────
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'ders-programim-ogrenci'
  AND p."Key" = 'menu.ders-programim.view'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = mi."Id" AND mip."PermissionId" = p."Id"
  );

-- ── 3. Student rolüne menu.ders-programim.view ata ─────────────────────
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'Student'
  AND p."Key" = 'menu.ders-programim.view'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── Doğrulama ──────────────────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug", mi."Route"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-ve-donem-islemleri'
ORDER BY mi."SortOrder";

COMMIT;
