-- Sidebar menü item etiketini güncelle: "Ders Açma İşlemleri" → "Ders Aç (Müfredat)"
-- MenuStructureSeed.cs sadece tablo boşsa seed atar; mevcut satırları güncellemez.
-- Bu yüzden manuel UPDATE gerekiyor.

UPDATE "MenuItems"
SET "Label" = 'Ders Aç (Müfredat)'
WHERE "Slug" = 'ders-acma-islemleri'
  AND "Label" = 'Ders Açma İşlemleri';

SELECT "Slug", "Label" FROM "MenuItems" WHERE "Slug" = 'ders-acma-islemleri';
