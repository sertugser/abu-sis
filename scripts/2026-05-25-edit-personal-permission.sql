-- Add menu.ogrenci-rehberi.edit-personal permission + grant to AdministrativeStaff (2026-05-25)
BEGIN;
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
SELECT 'menu.ogrenci-rehberi.edit-personal', 0, 'Öğrenci kişisel bilgisi düzenleme', NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.ogrenci-rehberi.edit-personal');
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r, "Permissions" p
WHERE r."Name" = 'AdministrativeStaff'
  AND p."Key" = 'menu.ogrenci-rehberi.edit-personal'
  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");
COMMIT;
