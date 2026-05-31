-- 2026-05-31: "Ders Kayıt Düzenleme" → "Ders Kayıt Onay" rename (slug + permission keys).
-- Şema değişmez; permission key'leri in-place güncellenir (FK PermissionId korunur).
BEGIN;

-- 1) Permission key'leri yeniden adlandır
UPDATE "Permissions" SET "Key" = 'menu.ders-kayit-onay.view'    WHERE "Key" = 'menu.ders-kayit-duzenleme.view';
UPDATE "Permissions" SET "Key" = 'menu.ders-kayit-onay.approve' WHERE "Key" = 'menu.ders-kayit-duzenleme.approve';
UPDATE "Permissions" SET "Key" = 'menu.ders-kayit-onay.reject'  WHERE "Key" = 'menu.ders-kayit-duzenleme.reject';

-- 2) Menü öğesi label/route/slug
UPDATE "MenuItems"
   SET "Label" = 'Ders Kayıt Onay',
       "Route" = '/ders-kayit-onay',
       "Slug"  = 'ders-kayit-onay',
       "UpdatedAt" = NOW()
 WHERE "Slug" = 'ders-kayit-duzenleme';

-- 3) Eski route'lu bildirim link'leri (opsiyonel tutarlılık)
UPDATE "Notifications"
   SET "Link" = REPLACE("Link", '/ders-kayit-duzenleme', '/ders-kayit-onay')
 WHERE "Link" LIKE '/ders-kayit-duzenleme%';

COMMIT;
