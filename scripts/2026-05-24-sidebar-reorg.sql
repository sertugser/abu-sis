-- ──────────────────────────────────────────────────────────────────────
-- 2026-05-24: Ders İşlemleri sidebar reorg + Sınav Müfredatı izin ayrımı
-- ──────────────────────────────────────────────────────────────────────
-- NEDEN MANUEL SQL: MenuStructureSeed.cs idempotent — tablo boş değilse
-- return ediyor. Seed C# değişiklikleri (sıra/label/modül) mevcut DB'ye
-- yansımaz; sadece yeni kurulumlarda geçerli.
--
-- ÇALIŞTIRMA SIRASI (ÖNEMLİ):
--   1) Kod değişiklikleri deploy + API RESTART → PermissionSeed merge
--      semantiği "menu.sinav-mufredati.view" ve ".edit" satırlarını
--      Permissions tablosuna otomatik ekler (yeni Permission EKLEMEZ bu script).
--   2) Bu script'i çalıştır → menü sırası + Sınav Müfredatı izin repoint +
--      RolePermission atamaları.
--
-- Yapılan değişiklikler:
--   1) "Ders Veren Öğretim Üyeleri" → "Ders Veren Öğretim Elemanları"
--      (Bölüm İşlemleri'ndeki kopya zaten "Elemanları"; tek isim sağlanır)
--   2) "Derslik Doluluk Tablosu" Ders İşlemleri → Kaynak Yönetimi (sortOrder 3)
--   3) Ders İşlemleri tail yeniden sıralanır; çift sortOrder=6 düzelir;
--      Sınav Müfredatı 6 → 14 (Program Onayı öncesi)
--   4) Sınav Müfredatı menü izni: menu.ders-acma-islemleri.view →
--      menu.sinav-mufredati.view (API guard'ları da kodda ayrıldı)
--   5) menu.sinav-mufredati.view + .edit → AdministrativeStaff +
--      DepartmentManager + Dean (mevcut erişimi koru, daraltma)
--
-- SystemAdministrator: kod-shortcut (Karar 4), DB satırı YOK.
-- (ModuleId, SortOrder) UNIQUE değil → UPDATE'lerde çakışma sorunu yok.
-- ──────────────────────────────────────────────────────────────────────

BEGIN;

-- ── 0. Ön kontrol: yeni permission'lar mevcut mu? (API restart sonrası dolu olmalı) ──
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Key" = 'menu.sinav-mufredati.view') THEN
    RAISE EXCEPTION 'menu.sinav-mufredati.view bulunamadı. Önce API restart edip PermissionSeed merge''i çalıştırın.';
  END IF;
END $$;

-- ── 1. İsim birleştirme: Ders Veren Öğretim Üyeleri → Elemanları ──
UPDATE "MenuItems"
SET "Label" = 'Ders Veren Öğretim Elemanları', "UpdatedAt" = NOW()
WHERE "Slug" = 'ders-veren-ogretim-uyeleri';

-- ── 2. Derslik Doluluk Tablosu → Kaynak Yönetimi modülüne taşı ──
UPDATE "MenuItems"
SET "ModuleId" = (SELECT "Id" FROM "MenuModules" WHERE "Slug" = 'kaynak-yonetimi'),
    "SortOrder" = 3,
    "UpdatedAt" = NOW()
WHERE "Slug" = 'derslik-doluluk';

-- ── 3. Ders İşlemleri tail yeniden sıralama (drift'e karşı slug bazlı, açık değerler) ──
UPDATE "MenuItems" SET "SortOrder" = 1,  "UpdatedAt" = NOW() WHERE "Slug" = 'ders-katalogu';
UPDATE "MenuItems" SET "SortOrder" = 2,  "UpdatedAt" = NOW() WHERE "Slug" = 'mufredat-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 3,  "UpdatedAt" = NOW() WHERE "Slug" = 'mufredat-sinif-donem';
UPDATE "MenuItems" SET "SortOrder" = 4,  "UpdatedAt" = NOW() WHERE "Slug" = 'secmeli-yapilandirma';
UPDATE "MenuItems" SET "SortOrder" = 5,  "UpdatedAt" = NOW() WHERE "Slug" = 'ders-acma-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 6,  "UpdatedAt" = NOW() WHERE "Slug" = 'donem-programi-aktarim';
UPDATE "MenuItems" SET "SortOrder" = 7,  "UpdatedAt" = NOW() WHERE "Slug" = 'subelendirme-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 8,  "UpdatedAt" = NOW() WHERE "Slug" = 'ders-kontenjan-islemleri';
UPDATE "MenuItems" SET "SortOrder" = 9,  "UpdatedAt" = NOW() WHERE "Slug" = 'ders-saatleri-yonetimi';
UPDATE "MenuItems" SET "SortOrder" = 10, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-programi-kisitlamalari';
UPDATE "MenuItems" SET "SortOrder" = 11, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-programi-olusturma';
UPDATE "MenuItems" SET "SortOrder" = 12, "UpdatedAt" = NOW() WHERE "Slug" = 'ogretim-elemani-atama';
UPDATE "MenuItems" SET "SortOrder" = 13, "UpdatedAt" = NOW() WHERE "Slug" = 'ders-veren-ogretim-uyeleri';
UPDATE "MenuItems" SET "SortOrder" = 14, "UpdatedAt" = NOW() WHERE "Slug" = 'sinav-mufredati';
UPDATE "MenuItems" SET "SortOrder" = 15, "UpdatedAt" = NOW() WHERE "Slug" = 'program-onayi';

-- ── 4. Sınav Müfredatı menü izni repoint: ders-acma-islemleri.view → sinav-mufredati.view ──
DELETE FROM "MenuItemPermissions"
WHERE "MenuItemId" = (SELECT "Id" FROM "MenuItems" WHERE "Slug" = 'sinav-mufredati');

INSERT INTO "MenuItemPermissions" ("MenuItemId", "PermissionId")
SELECT mi."Id", p."Id"
FROM "MenuItems" mi
CROSS JOIN "Permissions" p
WHERE mi."Slug" = 'sinav-mufredati'
  AND p."Key" = 'menu.sinav-mufredati.view';

-- ── 5. RolePermission: sinav-mufredati view+edit → AdministrativeStaff + DepartmentManager + Dean ──
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", p."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN "Permissions" p
WHERE r."Name" IN ('AdministrativeStaff', 'DepartmentManager', 'Dean')
  AND p."Key" IN ('menu.sinav-mufredati.view', 'menu.sinav-mufredati.edit')
  AND NOT EXISTS (
    SELECT 1 FROM "RolePermissions" rp
    WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  );

-- ── Doğrulama sorguları ──────────────────────────────────────────────
SELECT mi."SortOrder", mi."Label", mi."Slug"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'ders-islemleri'
ORDER BY mi."SortOrder";

SELECT mi."SortOrder", mi."Label", mi."Slug"
FROM "MenuItems" mi
JOIN "MenuModules" mm ON mm."Id" = mi."ModuleId"
WHERE mm."Slug" = 'kaynak-yonetimi'
ORDER BY mi."SortOrder";

COMMIT;
