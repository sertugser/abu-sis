-- Seed the student document-type reference catalog (8 rows) — idempotent backup (2026-05-25)
-- The DocumentTypeSeed.SeedAsync runs unconditionally on every API startup, so this
-- script is only a backup for environments where the seed pipeline is not executed.
BEGIN;

INSERT INTO "DocumentTypes" ("Code", "Name", "IsRequired", "SortOrder")
SELECT v."Code", v."Name", v."IsRequired", v."SortOrder"
FROM (VALUES
    ('LISE_DIPLOMA',      'Lise Diploması',           TRUE,  1),
    ('OSYS_SONUC',        'ÖSYS/YKS Sonuç Belgesi',   TRUE,  2),
    ('NUFUS',             'Nüfus Cüzdanı Fotokopisi', TRUE,  3),
    ('FOTO',              'Vesikalık Fotoğraf',       TRUE,  4),
    ('ASKERLIK',          'Askerlik Durum Belgesi',   FALSE, 5),
    ('SAGLIK',            'Sağlık Raporu',            FALSE, 6),
    ('DENKLIK',           'Denklik Belgesi',          FALSE, 7),
    ('ONCEKI_TRANSKRIPT', 'Önceki Kurum Transkripti', FALSE, 8)
) AS v("Code", "Name", "IsRequired", "SortOrder")
WHERE NOT EXISTS (SELECT 1 FROM "DocumentTypes" d WHERE d."Code" = v."Code");

COMMIT;
