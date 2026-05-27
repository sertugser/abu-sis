-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-27: planning-summary erişimini yönetim rollerine ver
-- ──────────────────────────────────────────────────────────────────────
-- BAĞLAM: ManagementDashboardPage (RoleDashboard → Dean / AdministrativeStaff /
-- DepartmentManager rollerine gösterilir) AcademicOperations planning-summary
-- endpoint'ini çağırır; bu endpoint [RequirePermission("menu.akilli-planlama.view")]
-- ile korunuyor. Ancak '.view' yalnızca Student rolüne verilmişti (Akıllı Ders
-- Önerileri sayfası için) — yani yönetim dashboard'unun planlama kartı bu 3 rol
-- için zaten 403 alıyordu (yalnızca SystemAdmin shortcut ile çalışıyordu). Öneri
-- sayfası kaldırılınca Student grant'ı da düştü; bu script doğru sahiplere verir.
--
-- Önceden var olan yanlış-bağ: planning-summary'nin '.view' permission key'i hâlâ
-- "akilli-planlama" namespace'inde (öneri sayfasından miras). İleride daha uygun
-- bir yönetim permission'ına taşınabilir; bu script minimal düzeltmedir.
--
-- Idempotent: NOT EXISTS ile mükerrer grant eklenmez.
-- ──────────────────────────────────────────────────────────────────────

INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", now()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('Dean', 'AdministrativeStaff', 'DepartmentManager')
  AND p."Key" = 'menu.akilli-planlama.view'
  AND NOT EXISTS (
      SELECT 1 FROM "RolePermissions" rp
      WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );
