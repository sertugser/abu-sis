-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-12: Şubelendirme İşlemleri — Otomatik şubelendirme (auto-split) izni
-- ──────────────────────────────────────────────────────────────────────
-- Yeni permission: menu.subelendirme-islemleri.auto-split
-- Backend endpoint: POST /api/course-opening/auto-split
-- Frontend: SectioningPage alt toolbar "Şubelendir" butonu
--
-- ATANAN ROLLER: DepartmentManager, Dean, AdministrativeStaff
-- ATANMAYAN: Instructor, Student (auto-split admin operasyonu)
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
--
-- 3 strateji destekleniyor:
--   - Equal (Eşit Parçala) — round-robin
--   - EqualRepeat (Tekrarlayanları Homojen Dağıt) — IsRepeated flag bazlı
--   - ModOddEven (MOD Tek/Çift) — sadece 2 şube ile
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES ('menu.subelendirme-islemleri.auto-split', 0, 'Otomatik şubelendirme (auto-split)', NOW())
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('DepartmentManager', 'Dean', 'AdministrativeStaff')
  AND p."Key" = 'menu.subelendirme-islemleri.auto-split'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

COMMIT;
