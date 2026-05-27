-- 2026-05-23 — ÖSYM Kayıt Takvimi + Program Kontenjan Bilgileri placeholder sayfalarının kaldırılması
--
-- İki Faz 4 stub sayfası (ÖSYM Kayıt Takvimi, Program Kontenjan Bilgileri) backend+frontend
-- tamamen silindi. MenuStructureSeed/PermissionSeed/RolePermissionSeed kod tarafında temizlendi,
-- ancak mevcut DB'lerde seed "yalnızca boşsa" çalıştığı için aşağıdaki DB satırları elle silinmeli.
-- Tablolar (OsymRegistrationCalendars, ProgramQuotas) ayrı EF migration ile düşürülür.
--
-- Idempotent: tekrar çalıştırılabilir (eşleşme yoksa 0 satır siler).

BEGIN;

-- 1) Menü item ↔ permission bağlarını kaldır (hem menu item, hem permission tarafından)
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
    SELECT "Id" FROM "MenuItems" WHERE "Slug" IN ('osym-kayit-takvimi', 'program-kontenjan')
)
OR "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" LIKE 'menu.osym-kayit-takvimi.%' OR "Key" LIKE 'menu.program-kontenjan.%'
);

-- 2) Sidebar menü item'larını sil
DELETE FROM "MenuItems"
WHERE "Slug" IN ('osym-kayit-takvimi', 'program-kontenjan');

-- 3) Rol-permission atamalarını sil
DELETE FROM "RolePermissions"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" LIKE 'menu.osym-kayit-takvimi.%' OR "Key" LIKE 'menu.program-kontenjan.%'
);

-- 4) Permission key'lerinin kendisini sil
DELETE FROM "Permissions"
WHERE "Key" LIKE 'menu.osym-kayit-takvimi.%' OR "Key" LIKE 'menu.program-kontenjan.%';

COMMIT;
