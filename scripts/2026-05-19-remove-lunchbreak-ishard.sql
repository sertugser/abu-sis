-- 2026-05-19 — Schedule.LunchBreak.IsHard SystemConfig satırı kaldırıldı.
-- H13 hard/soft toggle artık ConstraintDefinitions["H13"].Category tek otorite
-- (Kısıtlar sayfası → Zorunlu/Tercihli). RuleEngine ResolveCategory bu kolonu okur;
-- H13_LunchBreakRule + S5_LunchBreakSoftRule eski config.LunchBreakIsHard check'ini
-- artık kullanmıyor (bkz. SchedulingConstraintConfig field silindi).

DELETE FROM "SystemConfigs"
WHERE "Key" = 'Schedule.LunchBreak.IsHard';

-- Doğrulama: satır var mı?
SELECT "Key", "Value" FROM "SystemConfigs" WHERE "Key" = 'Schedule.LunchBreak.IsHard';
