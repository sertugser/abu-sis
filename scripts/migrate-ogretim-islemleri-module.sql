-- ────────────────────────────────────────────────────────────────────────────
-- Migration: Move 4 instructor/advisor pages to new "Öğretim İşlemleri" module
-- ────────────────────────────────────────────────────────────────────────────
-- Bağlam:
--   "Ders İşlemleri" modülü hem admin hem hoca sayfaları içeriyordu. SystemAdmin
--   shortcut yüzünden Derslerim/Ders Programım/Not Girişi/Ders Kısıtları admin'e
--   de görünüyordu — anlamsız UX. Bu sayfalar yeni "Öğretim İşlemleri" modülüne
--   taşındı, modüle AllowedRolesCsv ile admin/student/idari personel hariç tutuldu.
--
-- Idempotent: birden fazla kez çalıştırılabilir.
-- ────────────────────────────────────────────────────────────────────────────

BEGIN;

-- 1) Yeni modül "Öğretim İşlemleri" yoksa ekle
INSERT INTO "MenuModules" ("ParentId", "Slug", "Label", "Icon", "SortOrder", "IsActive", "AllowedRolesCsv", "CreatedAt", "UpdatedAt")
SELECT NULL, 'ogretim-islemleri', 'Öğretim İşlemleri', 'GraduationCap', 15, true,
       'Instructor,AcademicAdvisor,Dean,DepartmentManager', NOW(), NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "MenuModules" WHERE "Slug" = 'ogretim-islemleri'
);

-- 2) 4 item'ı yeni modüle taşı + SortOrder yeniden numaralandır
UPDATE "MenuItems" mi
SET "ModuleId" = (SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'ogretim-islemleri'),
    "SortOrder" = CASE mi."Slug"
        WHEN 'derslerim' THEN 1
        WHEN 'ders-programim' THEN 2
        WHEN 'not-girisi' THEN 3
        WHEN 'danisman-ders-kisitlari' THEN 4
        ELSE mi."SortOrder"
    END,
    "UpdatedAt" = NOW()
WHERE mi."Slug" IN ('derslerim', 'ders-programim', 'not-girisi', 'danisman-ders-kisitlari');

COMMIT;

-- Doğrulama:
SELECT mi."Slug", mi."Label", mi."SortOrder", m."Slug" AS modul_slug
FROM "MenuItems" mi JOIN "MenuModules" m ON m."Id" = mi."ModuleId"
WHERE mi."Slug" IN ('derslerim', 'ders-programim', 'not-girisi', 'danisman-ders-kisitlari')
ORDER BY mi."SortOrder";
