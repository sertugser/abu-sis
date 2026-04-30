-- ──────────────────────────────────────────────────────────────────────
-- TEST AMAÇLI FAKE DATA — Term 3 (Güc) için
-- Amaç: Ders Aç (Hareket) sayfasında kaynak dönem olarak Term 3'ü
-- seçince liste dolu görünsün, tüm yeni özellikler test edilebilsin.
--
-- KULLANIM: Önce bu script çalıştırılır → tarayıcıda test edilir →
-- bittikten sonra 2026-04-28-fake-test-data-DELETE.sql çalıştırılır.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 5 CourseSection (BM bölümü, Term 3 = Güc) ────────────────────────
-- Status: 1 = Active

-- 1. CENG101 — 2 şube (doluluk test'i için: %88 ve %75)
INSERT INTO "CourseSections"
  ("CourseId", "TermId", "SectionNumber", "Capacity", "EnrolledCount", "InstructorId", "Status", "IsFinalized", "CreatedAt")
VALUES
  (59, 3, 1, 40, 35, 1, 1, false, NOW()),
  (59, 3, 2, 40, 30, 2, 1, false, NOW());

-- 2. MATH101 — 1 şube, iç/dış kota'lı (popover "İleri" test'i)
INSERT INTO "CourseSections"
  ("CourseId", "TermId", "SectionNumber", "Capacity", "EnrolledCount", "InternalQuota", "ExternalQuota", "InstructorId", "Status", "IsFinalized", "CreatedAt")
VALUES
  (62, 3, 1, 50, 18, 35, 15, 1, 1, false, NOW());

-- 3. PHY101 — 1 şube, %98 doluluk (kırmızı renk test'i) + schedule eklenecek
INSERT INTO "CourseSections"
  ("CourseId", "TermId", "SectionNumber", "Capacity", "EnrolledCount", "InstructorId", "Status", "IsFinalized", "CreatedAt")
VALUES
  (63, 3, 1, 40, 39, 3, 1, false, NOW());

-- 4. ENG101 — 1 şube, normal doluluk (%63 yeşil)
INSERT INTO "CourseSections"
  ("CourseId", "TermId", "SectionNumber", "Capacity", "EnrolledCount", "InstructorId", "Status", "IsFinalized", "CreatedAt")
VALUES
  (61, 3, 1, 60, 38, 2, 1, false, NOW());

-- ── PHY101 için SectionSchedule + Classroom (schedule mirası test'i) ──
-- TimeSlot 56 (Pazartesi 09:00-09:50), Classroom 1 (AMFİ 1)
INSERT INTO "SectionSchedules" ("CourseSectionId", "TimeSlotId", "ClassroomId", "BlockType")
SELECT cs."Id", 56, 1, 0
FROM "CourseSections" cs
WHERE cs."CourseId" = 63 AND cs."TermId" = 3 AND cs."SectionNumber" = 1;

-- ── Doğrulama ────────────────────────────────────────────────────────
SELECT
  c."Code" AS "Ders",
  cs."SectionNumber" AS "Şube",
  cs."Capacity" AS "Kontenjan",
  cs."EnrolledCount" AS "Kayıtlı",
  cs."InternalQuota" AS "İç Kota",
  cs."ExternalQuota" AS "Dış Kota",
  cs."InstructorId" AS "Hoca",
  CASE WHEN ss."Id" IS NOT NULL THEN 'EVET' ELSE 'hayır' END AS "Schedule"
FROM "CourseSections" cs
JOIN "Courses" c ON c."Id" = cs."CourseId"
LEFT JOIN "SectionSchedules" ss ON ss."CourseSectionId" = cs."Id"
WHERE cs."TermId" = 3
ORDER BY c."Code", cs."SectionNumber";

COMMIT;
