-- Scholarship/placement/integration RBAC permissions + grants to AdministrativeStaff (2026-05-25)
-- Idempotent. Later tasks append more INSERT pairs before COMMIT.
BEGIN;

-- menu.ogrenci-rehberi.edit-scholarship
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
SELECT 'menu.ogrenci-rehberi.edit-scholarship', 0, 'Öğrenci burs bilgisi düzenleme', NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.ogrenci-rehberi.edit-scholarship');
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r, "Permissions" p
WHERE r."Name" = 'AdministrativeStaff'
  AND p."Key" = 'menu.ogrenci-rehberi.edit-scholarship'
  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

COMMIT;
