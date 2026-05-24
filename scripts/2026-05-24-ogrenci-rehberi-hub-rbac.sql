-- Öğrenci Rehberi Hub RBAC fix (2026-05-24)
-- 1) Remove broken Student grant of admin student-directory view
-- 2) Remove orphan eligibility permission (never enforced by any controller)
-- 3) Add edit-academic permission + grant to AdministrativeStaff
--
-- Notlar:
--   * PermissionCategory.Menu = 0 (SIS.Domain/Enums/PermissionCategory.cs).
--   * "Permissions"."CreatedAt" ve "RolePermissions"."GrantedAt" NOT NULL ve DB
--     default'u YOK; INSERT'lerde NOW() ile açıkça verilir.
--   * Tablo/kolon adları EF default naming ile birebir: "Permissions"
--     ("Id","Key","Category","Description","CreatedAt"), "RolePermissions"
--     ("Id","RoleId","PermissionId","SourceBundleId","GrantedAt","GrantedByUserId"),
--     "AspNetRoles" ("Id","Name",...).
BEGIN;

DELETE FROM "RolePermissions" rp
USING "Permissions" p, "AspNetRoles" r
WHERE rp."PermissionId" = p."Id"
  AND rp."RoleId" = r."Id"
  AND p."Key" = 'menu.ogrenci-rehberi.view'
  AND r."Name" = 'Student';

DELETE FROM "RolePermissions" rp
USING "Permissions" p
WHERE rp."PermissionId" = p."Id"
  AND p."Key" = 'menu.ogrenci-rehberi.eligibility';

DELETE FROM "Permissions" WHERE "Key" = 'menu.ogrenci-rehberi.eligibility';

INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
SELECT 'menu.ogrenci-rehberi.edit-academic', 0, 'Öğrenci akademik bilgisi düzenleme', NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.ogrenci-rehberi.edit-academic');

-- SourceBundleId/GrantedByUserId left NULL (manual grant)
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r, "Permissions" p
WHERE r."Name" = 'AdministrativeStaff'
  AND p."Key" = 'menu.ogrenci-rehberi.edit-academic'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

COMMIT;
