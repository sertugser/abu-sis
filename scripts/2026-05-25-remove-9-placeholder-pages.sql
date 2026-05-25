-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-25: 9 boş (placeholder) sayfanın kaldırılması
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN: Aşağıdaki 9 sayfa yalnızca "Yakında / Bu sayfa henüz aktif değil"
-- placeholder'ı gösteren stub'lardı; backend karşılıkları yoktu ve App.tsx
-- route'u dışında hiçbir yerde kullanılmıyorlardı.
--
--   1. EBYS İmza Yetkilileri            /ebys-imza-yetkilileri
--   2. Fakülte İmza Yükleme İşlemleri   /fakulte-imza-yukleme-islemleri
--   3. Kullanıcı Grupları Sınırlandırmalar /kullanici-grup-sinirlandirmalar
--   4. Kullanıcı Rapor Tasarım Grupları /kullanici-rapor-tasarim-gruplari
--   5. Toplu Ders Kaydı                 /toplu-ders-kaydi
--   6. Toplu Staj Danışman Atama        /toplu-staj-danisman-atama
--   7. Dersler Arası Öğrenci Transfer   /dersler-arasi-ogrenci-transfer
--   8. Üniversite Bilgileri             /universite-bilgileri
--   9. Kurumsal Entegrasyon Tanımları   /kurumsal-entegrasyon-tanimlari
--
-- ETKİ:
--   - 9 stub .tsx dosyası silindi
--   - App.tsx import + route blokları kaldırıldı
--   - MenuStructureSeed.cs menü item satırları kaldırıldı
--   - PermissionSeed.cs menu.<slug>.view key'leri kaldırıldı
--
-- DİKKAT: Menü item slug'ı ile permission key'i 2 sayfada farklı:
--   - Dersler Arası Transfer: slug 'dersler-arasi-transfer',
--     permission 'menu.dersler-arasi-ogrenci-transfer.view'
--   - Fakülte İmza: slug 'fakulte-imza-yukleme',
--     permission 'menu.fakulte-imza-yukleme.view'
--
-- NEDEN MANUEL SQL: Seed merge logic (PermissionSeed, MenuStructureSeed,
-- RolePermissionSeed) sadece INSERT eder, DELETE etmez. Seed dosyalarından
-- referansları kaldırmak DB'de var olan satırları silmez.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 1. MenuItemPermissions: menü item ↔ permission bağlarını sil ──
--     (FK cascade'e güvenmeden açıkça temizle)
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" IN (
    SELECT "Id" FROM "MenuItems" WHERE "Slug" IN (
        'toplu-staj-danisman-atama',
        'toplu-ders-kaydi',
        'dersler-arasi-transfer',
        'kullanici-grup-sinirlandirmalar',
        'kullanici-rapor-tasarim-gruplari',
        'universite-bilgileri',
        'kurumsal-entegrasyon-tanimlari',
        'ebys-imza-yetkilileri',
        'fakulte-imza-yukleme'
    )
)
OR "PermissionId" IN (
    SELECT "Id" FROM "Permissions" WHERE "Key" IN (
        'menu.toplu-staj-danisman-atama.view',
        'menu.toplu-ders-kaydi.view',
        'menu.dersler-arasi-ogrenci-transfer.view',
        'menu.kullanici-grup-sinirlandirmalar.view',
        'menu.kullanici-rapor-tasarim-gruplari.view',
        'menu.universite-bilgileri.view',
        'menu.kurumsal-entegrasyon-tanimlari.view',
        'menu.ebys-imza-yetkilileri.view',
        'menu.fakulte-imza-yukleme.view'
    )
);

-- ── 2. RolePermissions: permission referanslarını sil ──
DELETE FROM "RolePermissions"
WHERE "PermissionId" IN (
    SELECT "Id" FROM "Permissions" WHERE "Key" IN (
        'menu.toplu-staj-danisman-atama.view',
        'menu.toplu-ders-kaydi.view',
        'menu.dersler-arasi-ogrenci-transfer.view',
        'menu.kullanici-grup-sinirlandirmalar.view',
        'menu.kullanici-rapor-tasarim-gruplari.view',
        'menu.universite-bilgileri.view',
        'menu.kurumsal-entegrasyon-tanimlari.view',
        'menu.ebys-imza-yetkilileri.view',
        'menu.fakulte-imza-yukleme.view'
    )
);

-- ── 3. MenuItems: sidebar girişlerini sil ──
DELETE FROM "MenuItems" WHERE "Slug" IN (
    'toplu-staj-danisman-atama',
    'toplu-ders-kaydi',
    'dersler-arasi-transfer',
    'kullanici-grup-sinirlandirmalar',
    'kullanici-rapor-tasarim-gruplari',
    'universite-bilgileri',
    'kurumsal-entegrasyon-tanimlari',
    'ebys-imza-yetkilileri',
    'fakulte-imza-yukleme'
);

-- ── 4. Permissions: menu.<slug>.view key'lerini sil ──
DELETE FROM "Permissions" WHERE "Key" IN (
    'menu.toplu-staj-danisman-atama.view',
    'menu.toplu-ders-kaydi.view',
    'menu.dersler-arasi-ogrenci-transfer.view',
    'menu.kullanici-grup-sinirlandirmalar.view',
    'menu.kullanici-rapor-tasarim-gruplari.view',
    'menu.universite-bilgileri.view',
    'menu.kurumsal-entegrasyon-tanimlari.view',
    'menu.ebys-imza-yetkilileri.view',
    'menu.fakulte-imza-yukleme.view'
);

COMMIT;
