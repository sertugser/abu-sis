-- 2026-05-19 — Schedule.AllowSameCourseSectionOverlap SystemConfig satırı kaldırıldı.
-- H8 (Aynı Ders Şube Çakışması) için artık tek otorite ConstraintDefinitions["H8"]:
--   - Pasif → kural tamamen devre dışı (eski Allow=true semantiği)
--   - Aktif + Zorunlu → hard yasak (default)
--   - Aktif + Tercihli → soft penalty (H8 predicate hybrid: Hard/Soft tek class)
-- Solver SameCourseSectionEncoder + SameCourseSectionSoftEncoder + H8 predicate hepsi
-- ConstraintDefinitions.Category'yi okur — drift kalmadı.

DELETE FROM "SystemConfigs"
WHERE "Key" = 'Schedule.AllowSameCourseSectionOverlap';

-- Doğrulama
SELECT "Key", "Value" FROM "SystemConfigs" WHERE "Key" = 'Schedule.AllowSameCourseSectionOverlap';
