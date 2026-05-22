-- 2026-05-19 — Schedule.MaxLabBlockHours SystemConfigs satırı H6 Parameters JSON'a
-- taşındı (Kısıtlar sayfası → H6 "Ders Bileşen Çakışması" dialog tek otorite).
-- AlgorithmParametersPanel artık tamamen silindi.

DO $$
DECLARE
    current_value TEXT;
    current_params TEXT;
    new_params TEXT;
    parsed_int INT;
BEGIN
    SELECT "Value" INTO current_value
    FROM "SystemConfigs"
    WHERE "Key" = 'Schedule.MaxLabBlockHours';

    parsed_int := COALESCE(NULLIF(current_value, '')::INT, 2);
    IF parsed_int <= 0 THEN parsed_int := 2; END IF;

    SELECT "Parameters" INTO current_params
    FROM "ConstraintDefinitions"
    WHERE "Code" = 'H6';
    IF current_params IS NULL OR current_params = '' THEN
        current_params := '{}';
    END IF;

    new_params := jsonb_set(
        current_params::jsonb,
        '{MaxLabBlockHours}',
        to_jsonb(parsed_int)
    )::text;

    UPDATE "ConstraintDefinitions"
    SET "Parameters" = new_params, "UpdatedAt" = NOW()
    WHERE "Code" = 'H6';

    RAISE NOTICE 'H6 Parameters güncellendi: %', new_params;
END $$;

DELETE FROM "SystemConfigs" WHERE "Key" = 'Schedule.MaxLabBlockHours';

-- Doğrulama
SELECT "Code", "Parameters" FROM "ConstraintDefinitions" WHERE "Code" = 'H6';
SELECT "Key" FROM "SystemConfigs" WHERE "Key" = 'Schedule.MaxLabBlockHours';
