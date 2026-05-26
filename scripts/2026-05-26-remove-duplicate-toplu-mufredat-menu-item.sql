-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-26: Yinelenen "Toplu Müfredat Atama" menü item'ının kaldırılması
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN: /toplu-mufredat-atama route'u iki ayrı menü item'ı tarafından
-- gösteriliyordu:
--   1. Slug 'toplu-mufredat-atama-bolum' → Modül "Bölüm İşlemleri"
--   2. Slug 'toplu-mufredat-atama'       → Modül "Öğrenci İşlemleri"
--
-- Aynı sayfanın sidebar'da iki kez çıkmaması için frontend (useMenu.ts)
-- 'toplu-mufredat-atama-bolum' item'ını HARDCODE bir liste ile gizliyordu.
-- Bu, "menü tamamen DB-driven" kontratını bozuyordu.
--
-- ÇÖZÜM: Kopya item'ı (Bölüm İşlemleri altındaki) DB'den kaldır; görünür
-- kalan 'toplu-mufredat-atama' (Öğrenci İşlemleri) korunur. Frontend
-- hardcode'u ve MenuStructureSeed.cs satırı da kaldırıldı.
--
-- DİKKAT: 'menu.toplu-mufredat-atama.view' permission'ı KORUNUR — diğer
-- (görünür kalan) item hâlâ kullanıyor. Sadece kopya MenuItem satırı ve
-- onun MenuItemPermissions bağları silinir.
--
-- NEDEN MANUEL SQL: Seed merge logic sadece INSERT eder, DELETE etmez.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. Kopya item ↔ permission bağlarını sil ──
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
    SELECT "Id" FROM "MenuItems" WHERE "Slug" = 'toplu-mufredat-atama-bolum'
);

-- ── 2. Kopya MenuItem satırını sil ──
DELETE FROM "MenuItems" WHERE "Slug" = 'toplu-mufredat-atama-bolum';

COMMIT;
