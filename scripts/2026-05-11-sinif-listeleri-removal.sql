-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-11: "Sınıf Listeleri" sayfasının kaldırılması
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN: Sınıf Listeleri (/sinif-listeleri) ve Şubelendirme İşlemleri
-- (/subelendirme-islemleri) aynı backend hook'unu (useDepartmentClassLists)
-- ve aynı veriyi gösteriyordu. Şubelendirme İşlemleri sayfası tek kaynak
-- olarak Sınıf Listeleri'nin görsel diline taşındı; eski sayfa silindi.
--
-- ETKİ:
--   - ClassListsPage.tsx silindi
--   - /sinif-listeleri route App.tsx'ten kaldırıldı
--   - SectioningPage.tsx, ClassListsPage'in tasarımıyla yeniden yazıldı
--   - AcademicOperationsController.GetDepartmentClassLists permission'ı
--     menu.subelendirme-islemleri.view veya menu.ana-sayfa.view'e geçti
--     (ManagementDashboardPage da bu endpoint'i kullanıyor)
--   - InstructorController.GetSectionStudents permission'ı
--     menu.derslerim.view'e geçti (diğer instructor endpoint'leriyle tutarlı)
--
-- NEDEN MANUEL SQL: Seed merge logic (PermissionSeed, MenuStructureSeed,
-- RolePermissionSeed) sadece INSERT eder, DELETE etmez. Seed dosyalarından
-- referansları kaldırmak DB'de var olan satırları silmez.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. RolePermissions tablosundan permission referanslarını sil ──
DELETE FROM "RolePermissions"
WHERE "PermissionId" = (
    SELECT "Id" FROM "Permissions" WHERE "Key" = 'menu.sinif-listeleri.view'
);

-- ── 2. MenuItems tablosundan sidebar girişini sil ──
DELETE FROM "MenuItems" WHERE "Slug" = 'sinif-listeleri';

-- ── 3. Permissions tablosundan permission'ı sil ──
DELETE FROM "Permissions" WHERE "Key" = 'menu.sinif-listeleri.view';

COMMIT;
