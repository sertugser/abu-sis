-- Add menu.ogrenci-no-degistirme.change permission + grant to AdministrativeStaff (2026-05-24)
BEGIN;
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
SELECT 'menu.ogrenci-no-degistirme.change', 0, 'Öğrenci numarası değiştirme', NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.ogrenci-no-degistirme.change');
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r, "Permissions" p
WHERE r."Name" = 'AdministrativeStaff'
  AND p."Key" = 'menu.ogrenci-no-degistirme.change'
  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");
COMMIT;
