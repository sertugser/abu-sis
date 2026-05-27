-- 2026-05-24 — Constraint "Profiller" + "Analiz/İstatistik" özelliklerinin kaldırılması
--
-- Backend'de ekranı olmayan iki özellik (ConstraintProfile + ConstraintViolationSnapshot/analytics)
-- controller + service + entity + DTO seviyesinde tamamen silindi. PermissionSeed/RolePermissionSeed
-- kod tarafında temizlendi, ancak mevcut DB'lerde seed "yalnızca boşsa" çalıştığı için aşağıdaki
-- permission satırları elle silinmeli.
--
-- Tablolar (ConstraintProfiles, ConstraintProfileItems, ConstraintViolationSnapshots,
-- ConstraintViolationSnapshotDetails) ayrı EF migration ile düşürülür:
--   20260523220301_RemoveConstraintProfilesAndAnalytics
--
-- DİKKAT: Yalnız iki ölü key silinir (.snapshot + .apply). Aynı prefix'teki CANLI key'ler
-- (.view/.create/.edit/.delete/.toggle/.simulate/.exception*) KORUNUR — bu yüzden LIKE değil,
-- tam IN listesi kullanılır.
--
-- Idempotent: tekrar çalıştırılabilir (eşleşme yoksa 0 satır siler).

BEGIN;

-- 1) Rol-permission atamalarını sil
DELETE FROM "RolePermissions"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" IN (
        'menu.ders-programi-kisitlamalari.snapshot',
        'menu.ders-programi-kisitlamalari.apply'
    )
);

-- 2) Yetki paketi (bundle) ↔ permission bağlarını sil
DELETE FROM "PermissionBundleItems"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" IN (
        'menu.ders-programi-kisitlamalari.snapshot',
        'menu.ders-programi-kisitlamalari.apply'
    )
);

-- 3) Menü item ↔ permission bağlarını sil (varsa)
DELETE FROM "MenuItemPermissions"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" IN (
        'menu.ders-programi-kisitlamalari.snapshot',
        'menu.ders-programi-kisitlamalari.apply'
    )
);

-- 4) Permission key'lerinin kendisini sil
DELETE FROM "Permissions"
WHERE "Key" IN (
    'menu.ders-programi-kisitlamalari.snapshot',
    'menu.ders-programi-kisitlamalari.apply'
);

COMMIT;
