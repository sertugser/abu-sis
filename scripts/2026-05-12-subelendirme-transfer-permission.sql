-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-12: Şubelendirme İşlemleri — Öğrenciler arası transfer izni
-- ──────────────────────────────────────────────────────────────────────
-- Yeni permission: menu.subelendirme-islemleri.transfer
-- Backend endpoint: POST /api/course-opening/transfer-students
-- Frontend: SectioningPage alt toolbar "Şubeye Taşı" butonu
--
-- NEDEN MANUEL SQL: PermissionSeed merge semantiği permission satırını
-- otomatik ekler (backend restart sonrası), ama RolePermissionSeed
-- "all-or-nothing" — mevcut tabloda satır varsa yeni eklemez.
-- Bu yüzden role-permission bağlamayı manuel SQL ile yapıyoruz.
--
-- ATANAN ROLLER: DepartmentManager, Dean, AdministrativeStaff
-- ATANMAYAN: Instructor (hoca şube transferi yapmamalı), Student, vs.
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- Permission'ın DB'de olduğundan emin ol (yoksa ekle — backend zaten ekler ama defensive)
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES ('menu.subelendirme-islemleri.transfer', 0, 'Şubeler arası öğrenci taşıma', NOW())
ON CONFLICT ("Key") DO NOTHING;

-- 3 role için RolePermission ekle
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('DepartmentManager', 'Dean', 'AdministrativeStaff')
  AND p."Key" = 'menu.subelendirme-islemleri.transfer'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

COMMIT;
