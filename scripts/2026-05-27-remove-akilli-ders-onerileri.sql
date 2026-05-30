-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-27: "Akıllı Ders Önerileri" (öğrenci ders öneri motoru) kaldırıldı
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN: Öğrenci-yüzlü öneri sayfası (/akilli-planlama, SmartPlanningPage)
-- 5000+ öğrenci ölçeğinde çalışmıyordu (tek istek tüm öğrencilerin eligibility
-- context'ini + CGPA'sını hesaplıyor, ardından ders başına N+1 zorluk/kritik-yol
-- sorgusu) ve asıl katma değeri olan "zorluk skoru" geçmiş not verisi olmadan
-- kozmetik kalıyordu. Özellik tamamen kaldırıldı.
--
-- KOD ETKİSİ:
--   - Backend: RecommendationController + RecommendationService +
--     CourseDifficultyService + CriticalPathService + SIS.Application/Recommendation/
--     + RecommendationServiceTests silindi; Program.cs DI 3 satır kaldırıldı.
--   - Frontend: SmartPlanningPage + recommendation.{ts,api.ts} + useRecommendations
--     silindi; App.tsx route/import, constants.ts (ROUTES/QUERY_KEYS/IMPLEMENTED_ROUTES)
--     ve CourseCatalogPage "Akıllı Planlamaya Git" butonu kaldırıldı.
--   - Seed: PermissionSeed 3 ölü key kaldırıldı; RolePermissionSeed grant'ları
--     temizlendi; MenuStructureSeed menü item satırı kaldırıldı.
--
-- KORUNANLAR (paylaşımlı — başka özellikler kullanıyor, SİLİNMEZ):
--   - 'menu.akilli-planlama.view'     → AcademicOperations planning-summary + context
--   - 'menu.akilli-planlama.forecast' → ForecastController (Ders Talep Tahmini)
--   - 'menu.akilli-planlama.simulate' → ForecastController (Talep Simülasyonu)
--   NOT: .view yalnızca Student rolüne verilmişti ama planning-summary bir YÖNETİM
--   endpoint'i. Bu önceden var olan yanlış-bağ ayrıca değerlendirilmeli; aşağıda
--   Student'ın artık amaçsız .view grant'ı kaldırılıyor (permission key korunuyor).
--
-- NEDEN MANUEL SQL: Seed merge logic yalnızca INSERT eder, DELETE etmez. Seed
-- dosyalarından referansları kaldırmak DB'de var olan satırları silmez.
-- Idempotent: tekrar çalıştırmak güvenlidir.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. MenuItemPermissions: menü item ↔ permission bağları ──
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
    SELECT "Id" FROM "MenuItems" WHERE "Slug" = 'akilli-planlama'
)
OR "PermissionId" IN (
    SELECT "Id" FROM "Permissions" WHERE "Key" IN (
        'menu.akilli-planlama.recommendation',
        'menu.akilli-planlama.difficulty',
        'menu.akilli-planlama.critical-path'
    )
);

-- ── 2a. RolePermissions: 3 ölü key'in tüm rol grant'ları ──
DELETE FROM "RolePermissions"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions" WHERE "Key" IN (
        'menu.akilli-planlama.recommendation',
        'menu.akilli-planlama.difficulty',
        'menu.akilli-planlama.critical-path'
    )
);

-- ── 2b. RolePermissions: Student'ın artık amaçsız .view grant'ı ──
--        (.view permission KALIR — planning-summary endpoint'i kullanıyor)
DELETE FROM "RolePermissions"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Student')
  AND "PermissionId" = (
      SELECT "Id" FROM "Permissions" WHERE "Key" = 'menu.akilli-planlama.view'
  );

-- ── 3. MenuItems: sidebar girişi ──
DELETE FROM "MenuItems" WHERE "Slug" = 'akilli-planlama';

-- ── 4. Permissions: 3 ölü key (.view / .forecast / .simulate KALIR) ──
DELETE FROM "Permissions" WHERE "Key" IN (
    'menu.akilli-planlama.recommendation',
    'menu.akilli-planlama.difficulty',
    'menu.akilli-planlama.critical-path'
);

COMMIT;
