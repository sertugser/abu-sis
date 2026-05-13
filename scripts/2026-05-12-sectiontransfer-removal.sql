-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-12: SectionTransferPage (Şubeler Arası Öğrenci Transfer) silindi
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN: Sayfa stub'tu, hiç implement edilmemişti. Aynı operasyon Şubelendirme
-- İşlemleri sayfasında (`menu.subelendirme-islemleri.transfer`) inline yapılıyor.
--
-- ETKİ:
--   - SectionTransferPage.tsx silindi
--   - App.tsx route + import temizlendi
--   - lib/constants.ts ROUTES.SECTION_TRANSFER + IMPLEMENTED_ROUTES temizlendi
--   - PermissionSeed (menu.subeler-arasi-ogrenci-transfer.view) yorumlandı
--   - MenuStructureSeed line 193 silindi
--   - DB'deki menü kalemi + permission + role-permission satırları bu script'le silinir
--
-- NEDEN MANUEL SQL: Seed merge sadece INSERT yapar; permission ve menü
-- kayıtları DB'den kendiliğinden silinmez.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

DELETE FROM "RolePermissions"
WHERE "PermissionId" = (
    SELECT "Id" FROM "Permissions" WHERE "Key" = 'menu.subeler-arasi-ogrenci-transfer.view'
);

DELETE FROM "MenuItems" WHERE "Slug" = 'subeler-arasi-transfer';

DELETE FROM "Permissions" WHERE "Key" = 'menu.subeler-arasi-ogrenci-transfer.view';

COMMIT;
