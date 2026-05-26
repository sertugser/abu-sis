-- 2026-05-26 — Web Modül Ayarları temizliği
-- Kod tarafında hiçbir yerde okunmayan ölü legacy ayarları ve etkisiz (kozmetik)
-- "Sosyal Transkript Öğrenci Başvuru" modül kayıtlarını mevcut veritabanlarından siler.
-- Seed (SeedData.WebModuleSettings.cs) bu kayıtları artık üretmiyor; bu script
-- daha önce seed edilmiş ortamlardaki artık satırları temizler.

BEGIN;

-- 1) Hiçbir backend akışında okunmayan ölü legacy ayarlar
DELETE FROM "WebModuleSettings"
WHERE ("ModuleKey" = 'StudentPanel'    AND "Key" = 'CourseRegistrationEnabled')
   OR ("ModuleKey" = 'StudentPanel'    AND "Key" = 'DocumentRequestEnabled')
   OR ("ModuleKey" = 'InstructorPanel' AND "Key" = 'ShowStudentPhone')
   OR ("ModuleKey" = 'StaffPanel'      AND "Key" = 'DocumentWorkflowEnabled');

-- 2) Hiçbir akış tarafından gate olarak kullanılmayan kozmetik modül
DELETE FROM "WebModuleSettings"
WHERE "ModuleKey" = 'SocialTranscriptStudentApplication';

COMMIT;
