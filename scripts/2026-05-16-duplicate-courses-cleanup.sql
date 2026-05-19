-- ====================================================================
-- Duplicate course cleanup — 2026-05-16
--
-- Aynı kodun boşluklu (yeni, yanlış isimli) ve boşluksuz (eski, doğru
-- isimli) iki versiyonunu birleştirir. Canonical = boşluksuz (eski seed,
-- isimleri Ankara Bilim resmi müfredatı ile eşleşiyor).
--
-- Etki (dry-run ile doğrulandı):
--   - 96 Course (boşluklu) silinir
--   - 12 çakışan kabuk CourseSection silinir (term 7, boş şubeler)
--   - 161 CourseSection canonical Course'a taşınır
--   - 3 CourseAccessPolicy canonical Course'a taşınır
-- Toplam: 272 satır mutasyonu, tek transaction.
-- ====================================================================
BEGIN;

CREATE TEMP TABLE _duplicated AS
  SELECT REGEXP_REPLACE(c."Code",'\s+','','g') AS norm_code
  FROM "Courses" c
  GROUP BY REGEXP_REPLACE(c."Code",'\s+','','g')
  HAVING COUNT(*) > 1;

CREATE TEMP TABLE _mapping AS
  SELECT new_c."Id" AS new_id, old_c."Id" AS old_id
  FROM "Courses" new_c
  JOIN _duplicated d ON d.norm_code = REGEXP_REPLACE(new_c."Code",'\s+','','g')
  JOIN "Courses" old_c
    ON REGEXP_REPLACE(old_c."Code",'\s+','','g') = d.norm_code
   AND old_c."Code" NOT LIKE '% %'
  WHERE new_c."Code" LIKE '% %';

CREATE TEMP TABLE _conflicting_sections AS
  SELECT new_cs."Id" AS section_id
  FROM _mapping m
  JOIN "CourseSections" new_cs ON new_cs."CourseId" = m.new_id
  WHERE EXISTS (
    SELECT 1 FROM "CourseSections" old_cs
    WHERE old_cs."CourseId" = m.old_id
      AND old_cs."TermId" = new_cs."TermId"
      AND old_cs."SectionNumber" IS NOT DISTINCT FROM new_cs."SectionNumber"
  );

-- 0. Çakışan kabuk section'lara ait audit log'ları sil (245 satır, RESTRICT FK)
DELETE FROM "SchedulingAuditLogs"
 WHERE "SectionId" IN (SELECT section_id FROM _conflicting_sections);

-- 1. Çakışan kabuk section'ları sil (term 7, boş şubeler)
DELETE FROM "CourseSections" WHERE "Id" IN (SELECT section_id FROM _conflicting_sections);

-- 2. Kalan section'ları canonical Course'a taşı
UPDATE "CourseSections" cs
   SET "CourseId" = m.old_id
  FROM _mapping m
 WHERE cs."CourseId" = m.new_id;

-- 3. CourseAccessPolicies'i taşı
UPDATE "CourseAccessPolicies" ap
   SET "CourseId" = m.old_id
  FROM _mapping m
 WHERE ap."CourseId" = m.new_id;

-- 4. Boşluklu Course satırlarını sil
DELETE FROM "Courses" WHERE "Id" IN (SELECT new_id FROM _mapping);

-- Doğrulama: artık duplikat kalmamış olmalı
SELECT 'kalan duplikat' AS check, COUNT(*) AS rows
FROM (
  SELECT REGEXP_REPLACE("Code",'\s+','','g') AS k
  FROM "Courses"
  GROUP BY REGEXP_REPLACE("Code",'\s+','','g')
  HAVING COUNT(*) > 1
) x;

COMMIT;
