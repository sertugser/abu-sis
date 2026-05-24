-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-24: Müfredat Ders İntibak İşlemleri — yeni menü item + izinler
-- ──────────────────────────────────────────────────────────────────────
-- Standalone toplu intibak (PreviousCourse eşleştirmesi) sayfası eklendi.
-- MenuStructureSeed idempotent → mevcut DB'ye bu script ile yansıtılır.
--
-- ÇALIŞTIRMA SIRASI:
--   1) Kod deploy + API RESTART → PermissionSeed merge
--      "menu.mufredat-ders-intibak.view" ve ".edit" satırlarını ekler.
--   2) Bu script'i çalıştır.
--
-- Menü: "Müfredat Ders İntibak İşlemleri" → Ders İşlemleri grubu, sortOrder 13
--       (Öğretim Elemanı Atama'dan sonra; Ders Veren/Sınav Müf./Program Onayı +1 kayar)
-- İzin: view+edit → AdministrativeStaff + DepartmentManager + Dean (SysAdmin shortcut).
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 0. Ön kontrol: permission'lar mevcut mu? ──
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.mufredat-ders-intibak.view') THEN
    RAISE EXCEPTION 'menu.mufredat-ders-intibak.view bulunamadı. Önce API restart edip PermissionSeed merge''i çalıştırın.';
  END IF;
END $$;

-- ── 1. Tail +1 kaydır (13. sıraya intibak için yer aç) ──
UPDATE "MenuItems" SET "SortOrder" = 14, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-veren-ogretim-uyeleri';
UPDATE "MenuItems" SET "SortOrder" = 15, "UpdatedAt" = NOW() WHERE "Slug" = 'sinav-mufredati';
UPDATE "MenuItems" SET "SortOrder" = 16, "UpdatedAt" = NOW() WHERE "Slug" = 'program-onayi';

-- ── 2. Yeni menü item (idempotent) ──
INSERT INTO "MenuItems" ("ModuleId", "Label", "Route", "Slug", "SortOrder", "OpenInNewTab", "IsActive", "CreatedAt", "UpdatedAt")
SELECT mm."Id", 'Müfredat Ders İntibak İşlemleri', '/mufredat-ders-intibak', 'mufredat-ders-intibak', 13, false, true, NOW(), NOW()
FROM "MenuModules" mm
WHERE mm."Slug" = 'ders-islemleri'
  AND NOT EXISTS (SELECT 1 FROM "MenuItems" WHERE "Slug" = 'mufredat-ders-intibak');

-- ── 3. Menü item'a permission bağla (idempotent) ──
INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'mufredat-ders-intibak'
  AND p."Key" = 'menu.mufredat-ders-intibak.view'
  AND NOT EXISTS (
    SELECT 1 FROM "MenuItemPermissions" mip
    WHERE mip."MenuItemId" = mi."Id" AND mip."PermissionId" = p."Id"
  );

-- ── 4. RolePermission: view+edit → AdministrativeStaff + DepartmentManager + Dean ──
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('AdministrativeStaff', 'DepartmentManager', 'Dean')
  AND p."Key" IN ('menu.mufredat-ders-intibak.view', 'menu.mufredat-ders-intibak.edit')
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── Doğrulama ──
SELECT mi."SortOrder", mi."Label", mi."Slug"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-islemleri'
ORDER BY mi."SortOrder";

COMMIT;
