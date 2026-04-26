-- Personel İşlemleri modülünün ikonunu Briefcase → IdCard değiştir
-- (Bölüm İşlemleri zaten Briefcase kullanıyor, çakışma çözümü)

UPDATE "MenuModules"
SET "Icon" = 'IdCard',
    "UpdatedAt" = NOW()
WHERE "Slug" = 'personel-islemleri';

SELECT "Slug", "Label", "Icon" FROM "MenuModules"
WHERE "Slug" IN ('personel-islemleri', 'bolum-islemleri');
