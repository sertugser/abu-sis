-- Instructor rolune duyuru izinlerini ekler
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'Instructor'
  AND p."Key" IN (
      'menu.duyuru-yonetimi.view',
      'menu.duyuru-yonetimi.create',
      'menu.duyuru-yonetimi.edit',
      'menu.duyuru-yonetimi.delete'
  )
ON CONFLICT DO NOTHING;

-- Kontrol
SELECT r."Name", p."Key"
FROM "RolePermissions" rp
JOIN "AspNetRoles" r ON r."Id" = rp."RoleId"
JOIN "Permissions" p ON p."Id" = rp."PermissionId"
WHERE r."Name" = 'Instructor'
  AND p."Key" LIKE 'menu.duyuru%'
ORDER BY p."Key";
