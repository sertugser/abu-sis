-- Doğrulama: Personel İşlemleri modülü ve item'ı doğru yerde mi?

\echo '── Yeni modül kaydı ──'
SELECT "Id", "Slug", "Label", "Icon", "SortOrder", "AllowedRolesCsv", "ParentId"
FROM "MenuModules"
WHERE "Slug" = 'personel-islemleri';

\echo ''
\echo '── Personel Listesi item bilgisi ──'
SELECT mi."Slug" AS item_slug, mi."Label" AS item_label, mi."Route", mi."SortOrder",
       m."Slug" AS modul_slug, m."Label" AS modul_label
FROM "MenuItems" mi
JOIN "MenuModules" m ON m."Id" = mi."ModuleId"
WHERE mi."Slug" = 'personel-listesi';

\echo ''
\echo '── Eski modül (Yönetim → Kullanıcı İşlemleri) item listesi ──'
SELECT mi."Slug", mi."Label", mi."SortOrder"
FROM "MenuItems" mi
JOIN "MenuModules" m ON m."Id" = mi."ModuleId"
WHERE m."Slug" = 'yonetim-kullanici-islemleri'
ORDER BY mi."SortOrder";
