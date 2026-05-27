-- Grant per-student class promotion permissions to AdministrativeStaff (2026-05-24)
-- The permission keys already exist in "Permissions" (PermissionSeed.cs); this only
-- adds the missing RolePermissions grants for AdministrativeStaff (idempotent).
BEGIN;
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r, "Permissions" p
WHERE r."Name" = 'AdministrativeStaff'
  AND p."Key" IN (
    'menu.sinif-atlatma-islemleri.view',
    'menu.sinif-atlatma-islemleri.promote',
    'menu.sinif-atlatma-islemleri.rollback'
  )
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );
COMMIT;
