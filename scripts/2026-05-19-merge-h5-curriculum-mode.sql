-- 2026-05-19 — Schedule.CurriculumConflictMode SystemConfigs satırı H5 Parameters JSON'a
-- taşındı (Kısıtlar sayfası → H5 dropdown tek otorite). Algoritma Parametreleri sayfasından
-- bu ayar kaldırıldı.

DO $$
DECLARE
    current_mode TEXT;
    current_params TEXT;
    new_params TEXT;
BEGIN
    SELECT "Value" INTO current_mode
    FROM "SystemConfigs"
    WHERE "Key" = 'Schedule.CurriculumConflictMode';
    IF current_mode IS NULL THEN current_mode := 'curriculum'; END IF;

    SELECT "Parameters" INTO current_params
    FROM "ConstraintDefinitions"
    WHERE "Code" = 'H5';
    IF current_params IS NULL OR current_params = '' THEN
        current_params := '{}';
    END IF;

    new_params := jsonb_set(
        current_params::jsonb,
        '{CurriculumConflictMode}',
        to_jsonb(current_mode)
    )::text;

    UPDATE "ConstraintDefinitions"
    SET "Parameters" = new_params, "UpdatedAt" = NOW()
    WHERE "Code" = 'H5';

    RAISE NOTICE 'H5 Parameters güncellendi: %', new_params;
END $$;

DELETE FROM "SystemConfigs" WHERE "Key" = 'Schedule.CurriculumConflictMode';

-- Doğrulama
SELECT "Code", "Parameters" FROM "ConstraintDefinitions" WHERE "Code" = 'H5';
SELECT "Key" FROM "SystemConfigs" WHERE "Key" = 'Schedule.CurriculumConflictMode';
