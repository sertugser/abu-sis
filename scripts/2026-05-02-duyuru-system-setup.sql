-- ────────────────────────────────────────────────────────────────────────────
-- 2026-05-02: Duyuru sistemi setup
-- ────────────────────────────────────────────────────────────────────────────
-- 1) Öğretim modülüne "Duyurular" menu item ekle + permission bağla
-- 2) Instructor rolüne duyuru yönetimi izinleri (4 perm: view/create/edit/delete)
-- 3) AcademicAdvisor rolüne duyuru yönetimi izinleri (aynı 4 perm)
--
-- Idempotent: birden fazla kez çalıştırılabilir (ON CONFLICT / IF NOT EXISTS).
--
-- Birleştirme: önceden 3 ayrı dosyaydı:
--   - 2026-05-02-duyurular-ogretim-menu.sql        (Section 1)
--   - 2026-05-02-instructor-duyuru-permissions.sql (Section 2)
--   - 2026-05-02-advisor-duyuru-permissions.sql    (Section 3)
-- ────────────────────────────────────────────────────────────────────────────


-- ── Section 1: Öğretim modülüne "Duyurular" menu item ekle ──────────────────
DO $$
DECLARE
  v_module_id  INT;
  v_perm_id    INT;
  v_item_id    INT;
BEGIN
  SELECT "Id" INTO v_module_id FROM "MenuModules" WHERE "Slug" = 'ogretim-islemleri';
  SELECT "Id" INTO v_perm_id   FROM "Permissions" WHERE "Key"  = 'menu.duyurular.view';

  IF v_module_id IS NULL THEN
    RAISE EXCEPTION 'MenuModule ogretim-islemleri bulunamadi';
  END IF;

  UPDATE "MenuItems" SET "SortOrder" = 2 WHERE "Slug" = 'derslerim'               AND "ModuleId" = v_module_id;
  UPDATE "MenuItems" SET "SortOrder" = 3 WHERE "Slug" = 'ders-programim'          AND "ModuleId" = v_module_id;
  UPDATE "MenuItems" SET "SortOrder" = 4 WHERE "Slug" = 'yoklama-girisi'          AND "ModuleId" = v_module_id;
  UPDATE "MenuItems" SET "SortOrder" = 5 WHERE "Slug" = 'not-girisi'              AND "ModuleId" = v_module_id;
  UPDATE "MenuItems" SET "SortOrder" = 6 WHERE "Slug" = 'danisman-ders-kisitlari' AND "ModuleId" = v_module_id;

  IF NOT EXISTS (SELECT 1 FROM "MenuItems" WHERE "Slug" = 'duyurular-ogretim') THEN
    INSERT INTO "MenuItems" ("Label","Route","Slug","SortOrder","OpenInNewTab","ModuleId","IsActive","CreatedAt","UpdatedAt")
    VALUES ('Duyurular','/duyurular','duyurular-ogretim',1,false,v_module_id,true,NOW(),NOW())
    RETURNING "Id" INTO v_item_id;

    IF v_perm_id IS NOT NULL THEN
      INSERT INTO "MenuItemPermissions" ("MenuItemId","PermissionId") VALUES (v_item_id, v_perm_id);
    END IF;

    RAISE NOTICE 'Eklendi: ItemId=%', v_item_id;
  ELSE
    RAISE NOTICE 'Zaten mevcut, atlaniyor.';
  END IF;
END $$;


-- ── Section 2: Instructor rolüne duyuru izinleri ────────────────────────────
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


-- ── Section 3: AcademicAdvisor rolüne duyuru izinleri ───────────────────────
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" = 'AcademicAdvisor'
  AND p."Key" IN (
      'menu.duyuru-yonetimi.view',
      'menu.duyuru-yonetimi.create',
      'menu.duyuru-yonetimi.edit',
      'menu.duyuru-yonetimi.delete'
  )
ON CONFLICT DO NOTHING;


-- ── Doğrulama ───────────────────────────────────────────────────────────────
SELECT r."Name", p."Key"
FROM "RolePermissions" rp
JOIN "AspNetRoles" r ON r."Id" = rp."RoleId"
JOIN "Permissions" p ON p."Id" = rp."PermissionId"
WHERE r."Name" IN ('Instructor', 'AcademicAdvisor')
  AND p."Key" LIKE 'menu.duyuru%'
ORDER BY r."Name", p."Key";
