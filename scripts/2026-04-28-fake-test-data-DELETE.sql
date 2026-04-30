-- ──────────────────────────────────────────────────────────────────────
-- FAKE TEST DATA SİL — Term 3 (Güc) için eklenmiş test verisini temizler.
--
-- Term 3 başlangıçta tamamen boştu (0 section), bu yüzden
-- "Term 3'teki TÜM section'lar fake idi" kabul ederek toplu siliyoruz.
--
-- Cascade: SectionSchedules CourseSection'a bağlı (ON DELETE CASCADE)
-- yani manuel silmeye gerek yok ama net görünmesi için ayrı yazıldı.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- 1. SectionSchedule satırları (cascade da silerdi ama explicit)
DELETE FROM "SectionSchedules"
WHERE "CourseSectionId" IN (
    SELECT "Id" FROM "CourseSections" WHERE "TermId" = 3
);

-- 2. (Eğer fake data sırasında CourseAccessPolicy oluştuysa — şimdilik yok)
DELETE FROM "CourseAccessPolicies" WHERE "TermId" = 3;

-- 3. CourseSection satırları
DELETE FROM "CourseSections" WHERE "TermId" = 3;

-- ── Doğrulama: Term 3 boş olmalı ─────────────────────────────────────
SELECT COUNT(*) AS "Kalan Section (0 olmalı)" FROM "CourseSections" WHERE "TermId" = 3;

COMMIT;
