-- Remove menu.ders-degerlendirme.view from all roles except Student
-- Ders Değerlendirme sayfası yalnızca Student rolünde görünür (2026-05-02)

DELETE FROM "RolePermissions" rp
USING "AspNetRoles" r, "Permissions" p
WHERE rp."RoleId" = r."Id"
  AND rp."PermissionId" = p."Id"
  AND p."Key" = 'menu.ders-degerlendirme.view'
  AND r."Name" <> 'Student';
