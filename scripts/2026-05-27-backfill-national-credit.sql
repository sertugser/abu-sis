-- 2026-05-27 - Ulusal Kredi (NationalCredit) backfill
-- Problem: "Courses" tablosundaki NationalCredit alani tum derslerde 0 idi.
--   Kolon sonradan eklenmis (default 0), seed hic doldurmamis, mevcut veri backfill
--   edilmemis. Bu yuzden ders katalogundaki "K" (Ulusal Kredi) kolonu hep 0 gorunuyordu.
-- Cozum: YOK ulusal kredi konvansiyonu => Kredi = Teori + tavan(Lab/2).
--   Ankara Bilim resmi Bilgisayar Muh. mufredatiyla dogrulandi; ~40 dersin tamami
--   (tek anomali CENG336 haric) bu formulle birebir tutuyor. CENG336 web'de 4 idi
--   ama T3+U0 oldugundan formul geregi 3 birakildi (web kaynakli olasi tipo).
-- Kapsam: NationalCredit = 0 olan TUM dersler (idempotent; daha once set edilmis
--   degerlere dokunmaz).

BEGIN;

UPDATE "Courses"
SET "NationalCredit" = ("TheoryHours" + CEIL("LabHours" / 2.0))::int
WHERE "NationalCredit" = 0;

COMMIT;
