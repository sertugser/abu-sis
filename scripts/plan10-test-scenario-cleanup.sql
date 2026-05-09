-- Plan 10 Test Scenario — Geri alma script'i.
-- Term 7 ("2027-2028 Güz (Plan 10 Test)") ve ilgili tüm verileri siler.
-- İstediğin zaman çalıştırabilirsin: tek transaction, hata olursa rollback.

BEGIN;

-- 1) SectionSchedule satırları (apply edilmiş 64 section × 220 satır)
DELETE FROM "SectionSchedules"
WHERE "CourseSectionId" IN (
    SELECT "Id" FROM "CourseSections" WHERE "TermId" = 7
);

-- 2) CourseSection satırları (75 satır)
DELETE FROM "CourseSections" WHERE "TermId" = 7;

-- 3) ClassroomTermAssignment satırları (14 satır)
DELETE FROM "ClassroomTermAssignments" WHERE "TermId" = 7;

-- 4) TimeSlot satırları (55 satır)
DELETE FROM "TimeSlots" WHERE "TermId" = 7;

-- 5) AcademicTerm satırı
DELETE FROM "AcademicTerms" WHERE "Id" = 7;

-- 6) Doğrulama
SELECT
    (SELECT COUNT(*) FROM "AcademicTerms" WHERE "Id" = 7) AS term_remaining,
    (SELECT COUNT(*) FROM "TimeSlots" WHERE "TermId" = 7) AS slots_remaining,
    (SELECT COUNT(*) FROM "CourseSections" WHERE "TermId" = 7) AS sections_remaining,
    (SELECT COUNT(*) FROM "ClassroomTermAssignments" WHERE "TermId" = 7) AS classroom_assignments_remaining;

-- Tüm değerler 0 olmalı.

COMMIT;
