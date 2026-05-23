-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-24: RBAC izin boşlukları düzeltmesi
-- ──────────────────────────────────────────────────────────────────────
-- RolePermissionSeed yalnızca tablo BOŞken çalışır (idempotent guard), bu yüzden
-- mevcut canlı DB'ye yeni rol-izin satırları bu script ile eklenir. PermissionSeed
-- merge semantiği sayesinde Permission satırları zaten mevcut (yeni Permission EKLENMEZ).
--
-- Düzeltilen sorunlar:
--   1) Ders Kataloğu salt-okunur: hiçbir rol (SysAdmin hariç) ders ekleyemiyor/
--      düzenleyemiyordu. → AdministrativeStaff + DepartmentManager + Dean'e CRUD.
--   2a) Müfredat Sınıf Dönem: DepMgr+Dean'de create/edit/delete VARDI ama .view YOKTU
--       → liste açılmıyordu. → .view eklendi.
--   2b) Sınıf Atlatma Kuralları (promotion rules): hiçbir role atanmamıştı.
--       → DepartmentManager + Dean'e view/create/edit/delete.
--
-- NOT: lookup.faculty izni DB'ye EKLENMEZ — RequirePermissionAttribute içinde
--      "lookup.*" prefix shortcut'ı her authenticated user'a açar (Karar 7).
--
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1) Ders Kataloğu CRUD → AdministrativeStaff + DepartmentManager + Dean ──
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('AdministrativeStaff', 'DepartmentManager', 'Dean')
  AND p."Key" IN (
    'menu.ders-katalogu.create',
    'menu.ders-katalogu.edit',
    'menu.ders-katalogu.delete',
    'menu.ders-katalogu.prerequisite',
    'menu.ders-katalogu.equivalency',
    'menu.ders-katalogu.cross-list'
  )
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── 2a) Müfredat Sınıf Dönem görüntüleme → DepartmentManager + Dean ──
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('DepartmentManager', 'Dean')
  AND p."Key" = 'menu.mufredat-sinif-donem.view'
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── 2b) Sınıf Atlatma Kuralları (promotion rules) → DepartmentManager + Dean ──
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('DepartmentManager', 'Dean')
  AND p."Key" IN (
    'menu.sinif-atlatma-kurallari.view',
    'menu.sinif-atlatma-kurallari.create',
    'menu.sinif-atlatma-kurallari.edit',
    'menu.sinif-atlatma-kurallari.delete'
  )
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

COMMIT;

-- ── Doğrulama (opsiyonel) ──
-- SELECT r."Name", p."Key"
-- FROM "RolePermissions" rp
-- JOIN "AspNetRoles" r ON r."Id" = rp."RoleId"
-- JOIN "Permissions" p ON p."Id" = rp."PermissionId"
-- WHERE p."Key" IN (
--   'menu.ders-katalogu.create', 'menu.mufredat-sinif-donem.view', 'menu.sinif-atlatma-kurallari.view'
-- )
-- ORDER BY r."Name", p."Key";
