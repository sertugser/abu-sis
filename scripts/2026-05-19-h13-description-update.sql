-- H13 (Öğle Arası Koruması) açıklamasından "(hard mod aktifken)" ibaresi kaldırıldı.
-- SeedData.Constraints.cs sadece eksik Code'ları ekler; mevcut satırın Description'ını güncellemez.
-- Bu yüzden manuel UPDATE gerekiyor.

UPDATE "ConstraintDefinitions"
SET "Description" = 'Öğle arası saatlerine ders atanmasını engeller.'
WHERE "Code" = 'H13'
  AND "Description" = 'Öğle arası saatlerine ders atanmasını engeller (hard mod aktifken).';

SELECT "Code", "Name", "Description" FROM "ConstraintDefinitions" WHERE "Code" = 'H13';
