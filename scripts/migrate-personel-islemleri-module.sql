-- ────────────────────────────────────────────────────────────────────────────
-- Migration: "Personel Listesi" sayfasını yeni "Personel İşlemleri" top-level modülüne taşı
-- ────────────────────────────────────────────────────────────────────────────
-- Bağlam:
--   - MenuStructureSeed.cs idempotent (DB'de modül varsa hiç çalışmıyor)
--   - Yeni "Personel İşlemleri" top-level modülü seed'e eklendi (SortOrder=14)
--   - "Personel Listesi" item'ı eski yerinden (Yönetim → Kullanıcı İşlemleri)
--     yeni modüle ait olmalı
--
-- Bu script idempotent: birden fazla kez çalıştırılabilir (satır varsa atlar).
--
-- Çalıştırma:
--   psql -U <user> -d StudentInfoSystem -f scripts/migrate-personel-islemleri-module.sql
--
-- Sonrası: API restart ZORUNLU DEĞİL (MenuService cache'siz, her request'te query atıyor).
--          Frontend useMenu cache'i 5 dk → kullanıcılar logout/login ile anında görür.
-- ────────────────────────────────────────────────────────────────────────────

BEGIN;

-- 1) Yeni top-level modül "Personel İşlemleri" yoksa ekle
--    Icon = "IdCard" (kimlik kartı) — "Bölüm İşlemleri" ile aynı Briefcase çakışmasını önler
INSERT INTO "MenuModules" ("ParentId", "Slug", "Label", "Icon", "SortOrder", "IsActive", "AllowedRolesCsv", "CreatedAt", "UpdatedAt")
SELECT NULL, 'personel-islemleri', 'Personel İşlemleri', 'IdCard', 14, true, 'SystemAdministrator', NOW(), NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "MenuModules" WHERE "Slug" = 'personel-islemleri'
);

-- 2) "Personel Listesi" item'ını yeni modüle taşı + SortOrder = 1 (ilk ve tek item)
UPDATE "MenuItems"
SET "ModuleId" = (SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'personel-islemleri'),
    "SortOrder" = 1,
    "UpdatedAt" = NOW()
WHERE "Slug" = 'personel-listesi';

-- 3) Eski "Yönetim → Kullanıcı İşlemleri" altındaki diğer item'ların SortOrder'larını
--    yeniden numaralandır (eski 1,3,4,5,6,7,8 → yeni 1,2,3,4,5,6,7)
--    Personel Listesi (eski SortOrder=2) çıktığı için aradaki boşluğu kapatır.
UPDATE "MenuItems"
SET "SortOrder" = "SortOrder" - 1,
    "UpdatedAt" = NOW()
WHERE "ModuleId" = (SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'yonetim-kullanici-islemleri')
  AND "SortOrder" >= 3;

COMMIT;

-- Doğrulama sorguları (manuel çalıştır):
--
-- SELECT m."Slug", m."Label", m."SortOrder", m."AllowedRolesCsv"
-- FROM "MenuModules" m
-- WHERE m."Slug" = 'personel-islemleri';
--
-- SELECT mi."Slug", mi."Label", mi."Route", mi."SortOrder", m."Slug" AS "ModuleSlug"
-- FROM "MenuItems" mi
-- JOIN "MenuModules" m ON m."Id" = mi."ModuleId"
-- WHERE mi."Slug" = 'personel-listesi';
--
-- SELECT mi."Slug", mi."Label", mi."SortOrder"
-- FROM "MenuItems" mi
-- JOIN "MenuModules" m ON m."Id" = mi."ModuleId"
-- WHERE m."Slug" = 'yonetim-kullanici-islemleri'
-- ORDER BY mi."SortOrder";
