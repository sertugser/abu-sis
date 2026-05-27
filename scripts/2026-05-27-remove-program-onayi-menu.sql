-- "Program Onayı" sidebar menü item'ı kaldırılıyor.
-- Onay işlevi ayrı sayfa olmaktan çıkıp Ders Programı Oluşturma sayfasındaki
-- "Programı Onayla / Onayı Geri Al" butonuna taşındı (draft/publish modeli).
-- Permission key'leri (menu.program-onayi.view/approve/revoke) KORUNUR — buton
-- görünürlüğü ve onay/geri-al uçları için hâlâ kullanılıyor.
-- MenuStructureSeed idempotent ekleme yaptığından (mevcut item'ı silmez) bu satır
-- mevcut DB'lerden item'ı temizlemek için gereklidir.
DELETE FROM "MenuItems" WHERE "Slug" = 'program-onayi';
