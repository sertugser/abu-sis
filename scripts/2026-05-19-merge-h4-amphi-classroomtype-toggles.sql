-- 2026-05-19 — Schedule.AmphiThresholdEnabled + Schedule.ClassroomTypeRequirementEnabled
-- SystemConfigs satırları H4 ConstraintDefinition.Parameters JSON'a taşındı (mevcut değerler
-- korunarak), sonra SystemConfigs'ten silindi. Tek otorite = Kısıtlar sayfası → H4 dialog.

-- 1) Mevcut SystemConfig değerlerini oku (varsayılan false ise atla).
DO $$
DECLARE
    amphi_enabled TEXT;
    classroom_type_enabled TEXT;
    current_params TEXT;
    new_params TEXT;
BEGIN
    SELECT "Value" INTO amphi_enabled
    FROM "SystemConfigs"
    WHERE "Key" = 'Schedule.AmphiThresholdEnabled';

    SELECT "Value" INTO classroom_type_enabled
    FROM "SystemConfigs"
    WHERE "Key" = 'Schedule.ClassroomTypeRequirementEnabled';

    -- Varsayılan değerler boşsa false
    IF amphi_enabled IS NULL THEN amphi_enabled := 'false'; END IF;
    IF classroom_type_enabled IS NULL THEN classroom_type_enabled := 'false'; END IF;

    -- 2) H4 Parameters JSON'una iki bool key ekle (varsa override). Mevcut AmphiThreshold korunur.
    SELECT "Parameters" INTO current_params
    FROM "ConstraintDefinitions"
    WHERE "Code" = 'H4';

    IF current_params IS NULL OR current_params = '' THEN
        current_params := '{}';
    END IF;

    new_params := jsonb_set(
        jsonb_set(
            current_params::jsonb,
            '{AmphiThresholdEnabled}',
            to_jsonb(amphi_enabled::bool)
        ),
        '{ClassroomTypeRequirementEnabled}',
        to_jsonb(classroom_type_enabled::bool)
    )::text;

    UPDATE "ConstraintDefinitions"
    SET "Parameters" = new_params,
        "UpdatedAt" = NOW()
    WHERE "Code" = 'H4';

    RAISE NOTICE 'H4 Parameters güncellendi: %', new_params;
END $$;

-- 3) SystemConfigs satırlarını sil.
DELETE FROM "SystemConfigs"
WHERE "Key" IN ('Schedule.AmphiThresholdEnabled', 'Schedule.ClassroomTypeRequirementEnabled');

-- Doğrulama
SELECT "Code", "Parameters" FROM "ConstraintDefinitions" WHERE "Code" = 'H4';
SELECT "Key" FROM "SystemConfigs" WHERE "Key" LIKE '%AmphiThresholdEnabled%' OR "Key" LIKE '%ClassroomTypeRequirementEnabled%';
