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
