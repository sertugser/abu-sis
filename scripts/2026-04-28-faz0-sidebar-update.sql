-- ──────────────────────────────────────────────────────────────────────
-- Faz 0: Ders Aç (Hareket) — Sidebar Label + SortOrder Güncellemesi
-- Plan: docs/superpowers/plans/2026-04-28-ders-ac-hareket-refactor.md
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs idempotent — tablo boş değilse
-- return ediyor (satır 19-22). Seed C# kod değişikliği DB'ye yansımaz.
--
-- BULGULAR (2026-04-28 DB durumu):
--   - "Seçmeli Tipleri" DB'de YOK (seed'e sonradan eklenmiş ama idempotent
--     kontrolü yüzünden yansımamış) → bu script INSERT eder
--   - "Dönem Programı Aktarım" DB'de SortOrder=12, seed'de 13 → fark var
--   - ders-islemleri ModuleId = 62
--   - menu.secmeli-tipleri.view PermissionId = 1200
-- ──────────────────────────────────────────────────────────────────────
-- (ModuleId, SortOrder) UNIQUE değil — UPDATE'lerde çakışma olmaz.
-- (ModuleId, Route) UNIQUE — "/secmeli-tipleri" bu modülde yok, INSERT temiz.

BEGIN;

-- ── 1. Şubelendirme ve sonrası 2 sıra ileri kayar (Ders Aç (Hareket) ve Seçmeli Tipleri'ne yer açma) ──
UPDATE "MenuItems" SET "SortOrder" = 8,  "UpdatedAt" = NOW() WHERE "Slug" = 'subelendirme-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 9,  "UpdatedAt" = NOW() WHERE "Slug" = 'ders-kontenjan-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 10, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-saatleri-yonetimi';
UPDATE "MenuItems" SET "SortOrder" = 11, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-programi-kisitlamalari';
UPDATE "MenuItems" SET "SortOrder" = 12, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-programi-olusturma';
UPDATE "MenuItems" SET "SortOrder" = 13, "UpdatedAt" = NOW() WHERE "Slug" = 'ogretim-elemani-atama';

-- ── 2. Derslik Doluluk Tablosu ve sonrası 1 sıra ileri kayar (Seçmeli Tipleri INSERT'i için yer) ──
UPDATE "MenuItems" SET "SortOrder" = 14, "UpdatedAt" = NOW() WHERE "Slug" = 'derslik-doluluk';
UPDATE "MenuItems" SET "SortOrder" = 15, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-veren-ogretim-uyeleri';
UPDATE "MenuItems" SET "SortOrder" = 16, "UpdatedAt" = NOW() WHERE "Slug" = 'program-onayi';
UPDATE "MenuItems" SET "SortOrder" = 17, "UpdatedAt" = NOW() WHERE "Slug" = 'sinif-listeleri';

-- ── 3. Dönem Programı Aktarım: rename + 12 → 6 ──
UPDATE "MenuItems"
SET "Label" = 'Ders Aç (Hareket)', "SortOrder" = 6, "UpdatedAt" = NOW()
WHERE "Slug" = 'donem-programi-aktarim';

-- ── 4. Seçmeli Tipleri INSERT (DB'de yoktu) ──
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
VALUES (62, 'Seçmeli Tipleri', '/secmeli-tipleri', 'secmeli-tipleri', 7, false, true, NOW(), NOW());

-- ── 5. Yeni MenuItem'a permission bağla ──
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", 1200
FROM "MenuItems" mi
WHERE mi."Slug" = 'secmeli-tipleri';

-- ── Doğrulama sorgusu ────────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-islemleri'
ORDER BY mi."SortOrder";

COMMIT;
