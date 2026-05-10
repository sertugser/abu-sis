-- Plan 10 Test Scenario — User'ın oluşturduğu 42 ders × 75 section.
-- Yeni term: Fall 2027-2028 (Year=2027, TermType=Fall).
-- TimeSlot grid: Term 5 paterni (5 day × 11 slot = 55 slot, son 2 OnlineOnly).
-- CourseSection: capacity=30, instructor=NULL, classroom=NULL, status=Active(1).

BEGIN;

-- 1) Term
INSERT INTO "AcademicTerms" (
    "Name", "ShortName", "EnglishName", "ShortEnglishName", "OtherName",
    "Year", "TermType", "TermStart", "TermEnd",
    "YoksisStatus", "IsCancelled", "CreatedAt", "UpdatedAt",
    "OverrideStart", "OverrideEnd"
) VALUES (
    '2027-2028 Güz (Plan 10 Test)', '27-28 Güz Test', 'Fall 2027-2028 (Plan 10 Test)', '27-28 Fall Test', NULL,
    2027, 0, '2027-09-15 00:00:00+03', '2028-01-15 00:00:00+03',
    0, false, NOW(), NULL,
    NULL, NULL
)
RETURNING "Id" AS new_term_id;

-- 2) TimeSlots — copy Term 5's pattern for the new term.
INSERT INTO "TimeSlots" ("TermId", "DayOfWeek", "SlotOrder", "StartTime", "EndTime", "AllowedMode", "DisplayName")
SELECT
    (SELECT "Id" FROM "AcademicTerms" WHERE "Year" = 2027 AND "TermType" = 0 AND "Name" LIKE '%Plan 10%'),
    "DayOfWeek", "SlotOrder", "StartTime", "EndTime", "AllowedMode", "DisplayName"
FROM "TimeSlots"
WHERE "TermId" = 5;

-- 3) CourseSections — 75 section seed.
-- Each row: (CourseId, sectionNumber, capacity, status, IsPinned/Locked/Finalized=false, EnrolledCount=0)
INSERT INTO "CourseSections" (
    "CourseId", "TermId", "InstructorId", "Capacity", "EnrolledCount",
    "ClassroomId", "IsFinalized", "CreatedAt", "SectionNumber", "UpdatedAt",
    "IsScheduleLocked", "IsPinned", "ExternalQuota", "InternalQuota",
    "CrossListGroupId", "Status"
)
SELECT
    src.course_id,
    (SELECT "Id" FROM "AcademicTerms" WHERE "Year" = 2027 AND "TermType" = 0 AND "Name" LIKE '%Plan 10%'),
    NULL, 30, 0,
    NULL, false, NOW(), src.section_num, NULL,
    false, false, NULL, NULL,
    NULL, 1
FROM (VALUES
    -- (course_id, section_num)
    (136, 1), (136, 2),                                         -- ENG301 ×2
    (81, 1), (81, 2), (81, 3), (81, 4), (81, 5), (81, 6),       -- ENG201 ×6
    (276, 1), (276, 2), (276, 3), (276, 4), (276, 5),           -- MATH 101 Calculus I ×5
    (59, 1), (59, 2), (59, 3), (59, 4), (59, 5),                -- CENG101 Java I ×5
    (282, 1),                                                    -- MATH 224 Discrete Math ×1
    (280, 1),                                                    -- MATH 213 Differential Eq ×1
    (290, 1),                                                    -- BM 281 Sayisal Tasarim ×1
    (147, 1), (147, 2), (147, 3),                                -- SENG231 Data Science ×3
    (89, 1), (89, 2),                                            -- SENG321 Software Eng ×2
    (218, 1),                                                    -- EEE301 Telecomm I ×1
    (150, 1), (150, 2),                                          -- CENG361 Web Design ×2
    (222, 1),                                                    -- EEE321 DSP ×1 (user said EEE 309, mapped by name)
    (63, 1), (63, 2), (63, 3), (63, 4), (63, 5),                 -- PHY101 Physics I ×5
    (285, 1),                                                    -- BM 203 Programlama II ×1
    (311, 1), (311, 2), (311, 3), (311, 4),                      -- EEE 203 Digital Logic ×4
    (260, 1), (260, 2), (260, 3),                                -- BIO 101 Biology I ×3
    (95, 1),                                                     -- BM101 Java I ×1
    (368, 1),                                                    -- SENG 442 (Software Testing) ×1
    (119, 1),                                                    -- FIZ101 Fizik I ×1
    (163, 1),                                                    -- IE223 Materials Science ×1
    (211, 1),                                                    -- EEE201 Circuit Theory I ×1
    (301, 1), (301, 2),                                          -- CENG 241 Logic Design ×2
    (87, 1), (87, 2),                                            -- CENG301 Operating Systems ×2
    (366, 1),                                                    -- SENG 413 Distributed Systems ×1
    (101, 1),                                                    -- BM241 Programlama Dilleri ×1
    (300, 1), (300, 2), (300, 3), (300, 4),                      -- CENG 223 OOP ×4
    (298, 1), (298, 2), (298, 3), (298, 4),                      -- CENG 213 Data Structures ×4
    (350, 1),                                                    -- IE 365 Lean Mfg ×1
    (162, 1),                                                    -- IE227 Probability Theory ×1
    (170, 1),                                                    -- IE333 OR II ×1
    (357, 1),                                                    -- IE 499 Senior Design ×1
    (159, 1),                                                    -- IE101 Intro to IE ×1
    (161, 1),                                                    -- IE241 Engineering Economy ×1
    (169, 1),                                                    -- IE301 Facilities Planning ×1
    (173, 1),                                                    -- IE325 Quality Engineering ×1
    (220, 1),                                                    -- EEE313 Electronics II ×1 (user said EEE 305, mapped by name)
    (245, 1),                                                    -- EEE468 Numerical Methods ×1
    (221, 1),                                                    -- EEE331 Electrical Machines ×1
    (77, 1),                                                     -- EEE281 Electrical Circ. for Comp Eng ×1
    (247, 1)                                                     -- EEE431 Power System Analysis ×1 (user said EEE 472, mapped by name)
) AS src(course_id, section_num);

-- 4) Verify counts.
SELECT
    (SELECT "Id" FROM "AcademicTerms" WHERE "Year" = 2027 AND "TermType" = 0 AND "Name" LIKE '%Plan 10%') AS new_term_id,
    (SELECT COUNT(*) FROM "TimeSlots" WHERE "TermId" = (SELECT "Id" FROM "AcademicTerms" WHERE "Year" = 2027 AND "TermType" = 0 AND "Name" LIKE '%Plan 10%')) AS time_slots,
    (SELECT COUNT(*) FROM "CourseSections" WHERE "TermId" = (SELECT "Id" FROM "AcademicTerms" WHERE "Year" = 2027 AND "TermType" = 0 AND "Name" LIKE '%Plan 10%')) AS sections
;

COMMIT;
