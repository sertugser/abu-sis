-- 2026-05-26 — "Kullanıcı Yetki Grupları" sayfası izin standardizasyonu (M-1)
--
-- Sorun: Sayfa/menü "menu.kullanici-yetki-gruplari.view" ile korunuyordu ama veriyi
-- çeken API "system.permission-bundle.view" istiyordu. İki anahtar hiçbir role birlikte
-- atanmadığı için (SysAdmin shortcut hariç) tutarsızdı.
--
-- Çözüm: Menü item'ı system.permission-bundle.view'e bağla, eski menu.* anahtarını kaldır.
-- Idempotent — birden fazla çalıştırılabilir.
--
-- Yeni kurulumlarda gerekmez (seed zaten doğru bağlıyor); bu script yalnızca eski DB'leri normalize eder.

BEGIN;

-- 1) "Kullanıcı Yetki Grupları" menü item'ının izin bağını system.permission-bundle.view'e taşı.
WITH item AS (
    SELECT "Id" FROM "MenuItems" WHERE "Slug" = 'kullanici-yetki-gruplari'
),
new_perm AS (
    SELECT "Id" FROM "Permissions" WHERE "Key" = 'system.permission-bundle.view'
)
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT item."Id", new_perm."Id"
FROM item, new_perm
WHERE NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = item."Id" AND mip."PermissionId" = new_perm."Id"
);

-- 2) Eski menu.kullanici-yetki-gruplari.view bağını bu item'dan kaldır.
DELETE FROM "MenuItemPermissions" mip
USING "Permissions" p, "MenuItems" mi
WHERE mip."PermissionId" = p."Id"
  AND mip."MenuItemId" = mi."Id"
  AND mi."Slug" = 'kullanici-yetki-gruplari'
  AND p."Key" = 'menu.kullanici-yetki-gruplari.view';

-- 3) Artık kullanılmayan permission'ları temizle (varsa rol atamaları + menü bağları cascade/önce silinir).
--    menu.kullanici-yetki-gruplari.view ve hiç kullanılmayan menu.kullanici-yetki-gruplari.manage.
DELETE FROM "RolePermissions" rp
USING "Permissions" p
WHERE rp."PermissionId" = p."Id"
  AND p."Key" IN ('menu.kullanici-yetki-gruplari.view', 'menu.kullanici-yetki-gruplari.manage');

DELETE FROM "MenuItemPermissions" mip
USING "Permissions" p
WHERE mip."PermissionId" = p."Id"
  AND p."Key" IN ('menu.kullanici-yetki-gruplari.view', 'menu.kullanici-yetki-gruplari.manage');

DELETE FROM "Permissions"
WHERE "Key" IN ('menu.kullanici-yetki-gruplari.view', 'menu.kullanici-yetki-gruplari.manage');

COMMIT;
