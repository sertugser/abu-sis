-- ────────────────────────────────────────────────────────────────────────────
-- Migration: Rename transfer permission keys (short → long, canonical form)
-- ────────────────────────────────────────────────────────────────────────────
-- Bağlam: PermissionSeed/MenuStructureSeed/RolePermissionSeed eskiden iki ayrı
--         isim kullanıyordu. Standartlaşma için kısa ad → uzun ad rename.
--
-- Idempotent: birden fazla kez çalıştırılabilir.
-- ────────────────────────────────────────────────────────────────────────────

BEGIN;

-- 1) Eğer hem eski hem yeni key DB'de varsa (Task 1 + restart sonrası senaryosu):
--    yeni (boş, RolePermissions FK'si yok) key'i sil ki UPDATE'in unique constraint hatası vermesin
DELETE FROM "Permissions"
WHERE "Key" IN ('menu.subeler-arasi-ogrenci-transfer.view', 'menu.dersler-arasi-ogrenci-transfer.view')
  AND "Id" NOT IN (SELECT "PermissionId" FROM "RolePermissions");

-- 2) Eski kısa adı uzun ada yeniden adlandır (in-place, RolePermissions FK korunur)
UPDATE "Permissions" SET "Key" = 'menu.subeler-arasi-ogrenci-transfer.view'
WHERE "Key" = 'menu.subeler-arasi-transfer.view';

UPDATE "Permissions" SET "Key" = 'menu.dersler-arasi-ogrenci-transfer.view'
WHERE "Key" = 'menu.dersler-arasi-transfer.view';

COMMIT;

-- Doğrulama
SELECT "Key" FROM "Permissions" WHERE "Key" LIKE '%transfer%' ORDER BY "Key";
