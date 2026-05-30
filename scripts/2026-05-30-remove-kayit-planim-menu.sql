-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-30: "Kayıt Planım" sidebar item'ı kaldırıldı
-- ──────────────────────────────────────────────────────────────────────
-- Commit d9779358 (2026-05-28) MenuStructureSeed.cs'den item'ı düşürdü ama
-- seed idempotent — tablo doluysa return ediyor. Mevcut DB'lerde MenuItem
-- ve bağlı MenuItemPermissions hâlâ duruyor → sidebar'da hâlâ görünüyor.
--
-- Route /plan zaten App.tsx'te `<Navigate to="/ders-kayit" replace />` —
-- öğrenci tıklarsa yine de ders kayıt'a yönlenir. menu.plan.view permission
-- key'i ise korunur (App.tsx'teki Navigate redirect'i bu permission'la
-- gate'leniyor; kaldırılırsa eski deep-link'ler 403 olur).
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- Bağlı permission link'lerini sil
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
  SELECT "Id" FROM "MenuItems" WHERE "Slug" = 'plan'
);

-- MenuItem'ı sil
DELETE FROM "MenuItems" WHERE "Slug" = 'plan';

-- Doğrulama
SELECT mi."SortOrder", mi."Label", mi."Slug", mi."Route"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-ve-donem-islemleri'
ORDER BY mi."SortOrder";

COMMIT;
