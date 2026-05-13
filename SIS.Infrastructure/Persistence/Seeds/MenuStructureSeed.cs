using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// MenuModule + MenuItem aÄŸacÄ±nÄ±n seed'i. Idempotent (boÅŸ tabloda Ã§alÄ±ÅŸÄ±r).
/// Patch (post-Phase 1):
///   - "Genel Ä°ÅŸlemler" modÃ¼lÃ¼ Ã¶ÄŸrenci-merkezli: Akademik Takvim, Duyurular, AlÄ±nan Dersler
///   - "SÄ±k KullanÄ±lanlar" sadece Ana Sayfa + frontend favori injection
///   - "Duyuru YÃ¶netimi" modÃ¼lÃ¼nde admin iÃ§in ayrÄ± "Duyurular" link (route /duyurular,
///     slug "duyurular-admin", permission menu.duyurular-admin.view) â€” Route UNIQUE kalktÄ±
///   - Her modÃ¼le stabil Slug eklendi (MenuModule.Slug, UNIQUE)
/// </summary>
public static class MenuStructureSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        if (await context.MenuModules.AnyAsync(ct))
        {
            return; // idempotent
        }

        var permissions = await context.Permissions.ToDictionaryAsync(p => p.Key, ct);

        // â”€â”€â”€ ModÃ¼l 1: Genel Ä°ÅŸlemler (Patch 2C: SADECE Student) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var genelIslemler = new MenuModule
        {
            Slug = "genel-islemler",
            Label = "Genel Ä°ÅŸlemler",
            Icon = "Compass",
            SortOrder = 1,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(genelIslemler);
        await context.SaveChangesAsync(ct);

        genelIslemler.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Akademik Takvim",     "/akademik-takvim",      "akademik-takvim",     1, "menu.akademik-takvim.view"),
            BuildItem(permissions, "Duyurular",           "/duyurular",            "duyurular",           2, "menu.duyurular.view"),
            BuildItem(permissions, "Danışman Bilgileri",  "/danisman-bilgileri",   "danisman-bilgileri",  3, "menu.danisman-bilgileri.view"),
            BuildItem(permissions, "AlÄ±nan Dersler",      "/alinan-dersler",       "alinan-dersler",      4, "menu.alinan-dersler.view"),
        };

        // â”€â”€â”€ ModÃ¼l 2: SÄ±k KullanÄ±lanlar (favori-odaklÄ±, sadece Ana Sayfa default) â”€
        // Frontend favorites injection (useFavorites) ek item'lar push eder.
        var sikKullanilanlar = new MenuModule
        {
            Slug = "sik-kullanilanlar",
            Label = "SÄ±k KullanÄ±lanlar",
            Icon = "LayoutGrid",
            SortOrder = 2,
        };
        context.MenuModules.Add(sikKullanilanlar);
        await context.SaveChangesAsync(ct);

        sikKullanilanlar.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ana Sayfa", "/", "ana-sayfa", 1, "menu.ana-sayfa.view"),
        };

        // â”€â”€â”€ ModÃ¼l 3: Akademik Ä°ÅŸlemler (admin only) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var akademikIslemler = new MenuModule
        {
            Slug = "akademik-islemler",
            Label = "Akademik Ä°ÅŸlemler",
            Icon = "Calendar",
            SortOrder = 3,
        };
        context.MenuModules.Add(akademikIslemler);
        await context.SaveChangesAsync(ct);

        akademikIslemler.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Akademik DÃ¶nem TanÄ±mlarÄ±",       "/donem-yonetimi",                "akademik-donem",              1, "menu.akademik-donem.view"),
            BuildItem(permissions, "FakÃ¼lte TanÄ±mlarÄ±",              "/fakulte-tanimlari",             "fakulte-tanimlari",           2, "menu.fakulte-tanimlari.view"),
            BuildItem(permissions, "BÃ¶lÃ¼m TanÄ±mlarÄ±",                "/bolum-tanimlari",               "bolum-tanimlari",             3, "menu.bolum-tanimlari.view"),
            BuildItem(permissions, "Program TanÄ±mlarÄ±",              "/program-tanimlari",             "program-tanimlari",           4, "menu.program-tanimlari.view"),
            BuildItem(permissions, "Akademik Takvim",                "/akademik-takvim",               "akademik-takvim-goruntuleme", 5, "menu.akademik-takvim.view"),
            BuildItem(permissions, "Takvim OluÅŸturucu",              "/akademik-takvim-olusturma",     "akademik-takvim-olusturma",   6, "menu.akademik-takvim-olusturma.view"),
            BuildItem(permissions, "Ã–SYM KayÄ±t Takvimi",             "/osym-kayit-takvimi",            "osym-kayit-takvimi",          7, "menu.osym-kayit-takvimi.view"),
            BuildItem(permissions, "Program Kontenjan Bilgileri",    "/program-kontenjan",             "program-kontenjan",           8, "menu.program-kontenjan.view"),
        };

        // â”€â”€â”€ ModÃ¼l 4: DanÄ±ÅŸmanlÄ±k Ä°ÅŸlemleri (Phase 1'de boÅŸ â€” Karar #6) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var danismanlikIslemleri = new MenuModule
        {
            Slug = "danismanlik-islemleri",
            Label = "DanÄ±ÅŸmanlÄ±k Ä°ÅŸlemleri",
            Icon = "Users",
            SortOrder = 4,
        };
        context.MenuModules.Add(danismanlikIslemleri);
        await context.SaveChangesAsync(ct);

        // â”€â”€â”€ ModÃ¼l 5: Ders ve DÃ¶nem Ä°ÅŸlemleri (Patch 2C: SADECE Student) â”€â”€â”€â”€â”€â”€â”€â”€
        var dersVeDonemIslemleri = new MenuModule
        {
            Slug = "ders-ve-donem-islemleri",
            Label = "Ders ve DÃ¶nem Ä°ÅŸlemleri",
            Icon = "BookOpen",
            SortOrder = 5,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(dersVeDonemIslemleri);
        await context.SaveChangesAsync(ct);

        dersVeDonemIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ders KayÄ±t",              "/ders-kayit",            "ders-kayit",              1, "menu.ders-kayit.view"),
            BuildItem(permissions, "AkÄ±llÄ± Ders Ã–nerileri",   "/akilli-planlama",       "akilli-planlama",         2, "menu.akilli-planlama.view"),
            BuildItem(permissions, "KayÄ±t PlanÄ±m",            "/plan",                  "plan",                    3, "menu.plan.view"),
            BuildItem(permissions, "Not Listesi",             "/not-listesi",           "not-listesi",             4, "menu.not-listesi.view"),
            BuildItem(permissions, "DevamsÄ±zlÄ±k Durumu",      "/devamsizlik-durumu",    "devamsizlik-durumu",      5, "menu.devamsizlik-durumu.view"),
            BuildItem(permissions, "DÃ¶nem OrtalamalarÄ±",      "/donem-ortalamalari",    "donem-ortalamalari",      6, "menu.donem-ortalamalari.view"),
            BuildItem(permissions, "MÃ¼fredat Durum",          "/mufredat-durum",        "mufredat-durum",          7, "menu.mufredat-durum.view"),
            BuildItem(permissions, "Transkript",              "/report/transkript",     "transkript",              8, "menu.transkript.view", openInNewTab: true),
            BuildItem(permissions, "Transkript Senaryosu",    "/transkript-senaryosu",  "transkript-senaryosu",    9, "menu.transkript-senaryosu.view"),
        };

        // â”€â”€â”€ ModÃ¼l 6: Ders Ä°ÅŸlemleri (admin + instructor + advisor) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var dersIslemleri = new MenuModule
        {
            Slug = "ders-islemleri",
            Label = "Ders Ä°ÅŸlemleri",
            Icon = "BookOpen",
            SortOrder = 6,
        };
        context.MenuModules.Add(dersIslemleri);
        await context.SaveChangesAsync(ct);

        dersIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ders KataloÄŸu",                  "/ders-katalogu",                       "ders-katalogu",                  1,  "menu.ders-katalogu.view"),
            BuildItem(permissions, "MÃ¼fredat Ä°ÅŸlemleri",             "/mufredat-islemleri",                  "mufredat-islemleri",             2,  "menu.mufredat-islemleri.view"),
            BuildItem(permissions, "MÃ¼fredat SÄ±nÄ±f DÃ¶nem TanÄ±mlarÄ±", "/mufredat-sinif-donem-tanimlari",      "mufredat-sinif-donem",           3,  "menu.mufredat-sinif-donem.view"),
            BuildItem(permissions, "SeÃ§meli Ders YapÄ±landÄ±rmasÄ±",       "/secmeli-yapilandirma",                "secmeli-yapilandirma",           4,  "menu.secmeli-yapilandirma.view"),
            BuildItem(permissions, "Ders AÃ§ (MÃ¼fredat)",             "/ders-acma-islemleri",                 "ders-acma-islemleri",            5,  "menu.ders-acma-islemleri.view"),
            BuildItem(permissions, "Sınav Müfredatı",                  "/sinav-mufredati",                    "sinav-mufredati",                 6,  "menu.ders-acma-islemleri.view"),
            BuildItem(permissions, "Ders AÃ§ (Hareket)",              "/donem-programi-aktarim",              "donem-programi-aktarim",         6,  "menu.donem-programi-aktarim.view"),
            BuildItem(permissions, "Åubelendirme Ä°ÅŸlemleri",         "/subelendirme-islemleri",              "subelendirme-islemleri",         7,  "menu.subelendirme-islemleri.view"),
            BuildItem(permissions, "Ders Kontenjan Ä°ÅŸlemleri",       "/ders-kontenjan-islemleri",            "ders-kontenjan-islemleri",       8,  "menu.ders-kontenjan-islemleri.view"),
            BuildItem(permissions, "Ders Saatleri YÃ¶netimi",         "/ders-saatleri-yonetimi",              "ders-saatleri-yonetimi",         9,  "menu.ders-saatleri-yonetimi.view"),
            BuildItem(permissions, "Program KÄ±sÄ±tlamalarÄ±",          "/ders-programi-kisitlamalari",         "ders-programi-kisitlamalari",    10, "menu.ders-programi-kisitlamalari.view"),
            BuildItem(permissions, "Ders ProgramÄ± OluÅŸturma",        "/ders-programi-olusturma",             "ders-programi-olusturma",        11, "menu.ders-programi-olusturma.view"),
            BuildItem(permissions, "Ã–ÄŸretim ElemanÄ± Atama",          "/ogretim-elemani-atama",               "ogretim-elemani-atama",          12, "menu.ogretim-elemani-atama.view"),
            BuildItem(permissions, "Derslik Doluluk Tablosu",        "/derslik-doluluk-tablosu",             "derslik-doluluk",                13, "menu.derslik-doluluk.view"),
            BuildItem(permissions, "Ders Veren Ã–ÄŸretim Ãœyeleri",     "/ders-veren-ogretim-uyeleri",          "ders-veren-ogretim-uyeleri",     14, "menu.ders-veren-ogretim-uyeleri.view"),
            BuildItem(permissions, "Program OnayÄ±",                  "/program-onayi",                       "program-onayi",                  15, "menu.program-onayi.view"),
        };

        // â”€â”€â”€ ModÃ¼l 7: BÃ¶lÃ¼m Ä°ÅŸlemleri (hoca + admin iÃ§in) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Patch 2B: Proliz tarzÄ± hoca paneli â€” bÃ¶lÃ¼m-level operasyonlar
        var bolumIslemleri = new MenuModule
        {
            Slug = "bolum-islemleri",
            Label = "BÃ¶lÃ¼m Ä°ÅŸlemleri",
            Icon = "Briefcase",
            SortOrder = 7,
        };
        context.MenuModules.Add(bolumIslemleri);
        await context.SaveChangesAsync(ct);

        bolumIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Åubelendirme Ä°ÅŸlemleri",         "/subelendirme-islemleri",         "subelendirme-islemleri-bolum",      1, "menu.subelendirme-islemleri.view"),
            BuildItem(permissions, "Ders Veren Ã–ÄŸretim ElemanlarÄ±",  "/ders-veren-ogretim-uyeleri",     "ders-veren-ogretim-uyeleri-bolum",  2, "menu.ders-veren-ogretim-uyeleri.view"),
            BuildItem(permissions, "Toplu DanÄ±ÅŸman Atama",           "/toplu-danisman-atama",           "toplu-danisman-atama-bolum",        3, "menu.toplu-danisman-atama.view"),
            BuildItem(permissions, "Toplu MÃ¼fredat Atama",           "/toplu-mufredat-atama",           "toplu-mufredat-atama-bolum",        4, "menu.toplu-mufredat-atama.view"),
        };

        // â”€â”€â”€ ModÃ¼l 8: Ã–ÄŸrenci Ä°ÅŸlemleri (kayma: 7 â†’ 8) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var ogrenciIslemleri = new MenuModule
        {
            Slug = "ogrenci-islemleri",
            Label = "Ã–ÄŸrenci Ä°ÅŸlemleri",
            Icon = "Users",
            SortOrder = 8,
        };
        context.MenuModules.Add(ogrenciIslemleri);
        await context.SaveChangesAsync(ct);

        ogrenciIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ã–ÄŸrenci Rehberi",                "/ogrenci-rehberi",                     "ogrenci-rehberi",                1,  "menu.ogrenci-rehberi.view"),
            BuildItem(permissions, "Ã–ÄŸrenci No DeÄŸiÅŸtirme",          "/ogrenci-no-degistirme",               "ogrenci-no-degistirme",          2,  "menu.ogrenci-no-degistirme.view"),
            BuildItem(permissions, "SÄ±nÄ±f Atlatma Ä°ÅŸlemleri",        "/sinif-atlatma-islemleri",             "sinif-atlatma-islemleri",        3,  "menu.sinif-atlatma-islemleri.view"),
            BuildItem(permissions, "Toplu DanÄ±ÅŸman Atama",           "/toplu-danisman-atama",                "toplu-danisman-atama",           4,  "menu.toplu-danisman-atama.view"),
            BuildItem(permissions, "Toplu Staj DanÄ±ÅŸman Atama",      "/toplu-staj-danisman-atama",           "toplu-staj-danisman-atama",      5,  "menu.toplu-staj-danisman-atama.view"),
            BuildItem(permissions, "Toplu MÃ¼fredat Atama",           "/toplu-mufredat-atama",                "toplu-mufredat-atama",           6,  "menu.toplu-mufredat-atama.view"),
            BuildItem(permissions, "Toplu Ders KaydÄ±",               "/toplu-ders-kaydi",                    "toplu-ders-kaydi",               7,  "menu.toplu-ders-kaydi.view"),
            BuildItem(permissions, "Ders KayÄ±t DÃ¼zenleme",           "/ders-kayit-duzenleme",                "ders-kayit-duzenleme",           8,  "menu.ders-kayit-duzenleme.view"),
            BuildItem(permissions, "Dersler ArasÄ± Ã–ÄŸrenci Transfer", "/dersler-arasi-ogrenci-transfer",      "dersler-arasi-transfer",         10, "menu.dersler-arasi-ogrenci-transfer.view"),
            BuildItem(permissions, "Harf Notu DÃ¼zenleme",            "/harf-notu-duzenleme",                 "harf-notu-duzenleme",            11, "menu.harf-notu-duzenleme.view"),
        };

        // â”€â”€â”€ ModÃ¼l 9: Anketler (kayma: 8 â†’ 9) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var anketler = new MenuModule
        {
            Slug = "anketler",
            Label = "Anketler",
            Icon = "ClipboardCheck",
            SortOrder = 9,
        };
        context.MenuModules.Add(anketler);
        await context.SaveChangesAsync(ct);

        anketler.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ders DeÄŸerlendirme",             "/ders-degerlendirme",                  "ders-degerlendirme",             1, "menu.ders-degerlendirme.view"),
            BuildItem(permissions, "DeÄŸerlendirme SonuÃ§larÄ±",        "/degerlendirme-sonuclari",             "degerlendirme-sonuclari",        2, "menu.degerlendirme-sonuclari.view"),
            BuildItem(permissions, "DeÄŸerlendirme DÃ¶nemi YÃ¶netimi",  "/degerlendirme-donemi-yonetimi",       "degerlendirme-donemi-yonetimi",  3, "menu.degerlendirme-donemi-yonetimi.view"),
        };

        // â”€â”€â”€ ModÃ¼l 10: Kaynak YÃ¶netimi (kayma: 9 â†’ 10) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var kaynakYonetimi = new MenuModule
        {
            Slug = "kaynak-yonetimi",
            Label = "Kaynak YÃ¶netimi",
            Icon = "School",
            SortOrder = 10,
        };
        context.MenuModules.Add(kaynakYonetimi);
        await context.SaveChangesAsync(ct);

        kaynakYonetimi.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Derslik YÃ¶netimi",               "/derslik-yonetimi",                    "derslik-yonetimi",               1, "menu.derslik-yonetimi.view"),
            BuildItem(permissions, "Derslik-FakÃ¼lte AtamasÄ±",        "/derslik-fakulte-atamasi",             "derslik-fakulte-atamasi",        2, "menu.derslik-fakulte-atamasi.view"),
        };

        // â”€â”€â”€ ModÃ¼l 11: Duyuru YÃ¶netimi (kayma: 10 â†’ 11) â€” DUYURULAR LINKÄ° DE BURADA â”€
        var duyuruYonetimi = new MenuModule
        {
            Slug = "duyuru-yonetimi-grubu",
            Label = "Duyuru YÃ¶netimi",
            Icon = "Megaphone",
            SortOrder = 11,
        };
        context.MenuModules.Add(duyuruYonetimi);
        await context.SaveChangesAsync(ct);

        duyuruYonetimi.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Duyurular",                      "/duyurular",                           "duyurular-admin",                1, "menu.duyurular-admin.view"),
            BuildItem(permissions, "Duyuru YÃ¶netimi",                "/duyuru-yonetimi",                     "duyuru-yonetimi",                2, "menu.duyuru-yonetimi.view"),
            BuildItem(permissions, "Duyuru Kategorileri",            "/duyuru-kategorileri",                 "duyuru-kategorileri",            3, "menu.duyuru-kategorileri.view"),
        };

        // â”€â”€â”€ ModÃ¼l 12: HesabÄ±m (Patch 2C: SADECE Student) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var hesabim = new MenuModule
        {
            Slug = "hesabim",
            Label = "HesabÄ±m",
            Icon = "User",
            SortOrder = 12,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(hesabim);
        await context.SaveChangesAsync(ct);

        hesabim.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Åifre DeÄŸiÅŸtir",                 "/sifre-degistir",                      "sifre-degistir",                 1, "menu.sifre-degistir.view"),
        };

        // â”€â”€â”€ ModÃ¼l 13: YÃ¶netim (kayma: 12 â†’ 13, nested parent) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var yonetim = new MenuModule
        {
            Slug = "yonetim",
            Label = "YÃ¶netim",
            Icon = "Shield",
            SortOrder = 13,
        };
        context.MenuModules.Add(yonetim);
        await context.SaveChangesAsync(ct);

        // â”€â”€â”€ ModÃ¼l 12.1: YÃ¶netim â†’ KullanÄ±cÄ± Ä°ÅŸlemleri â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var yonetimKullanici = new MenuModule
        {
            ParentId = yonetim.Id,
            Slug = "yonetim-kullanici-islemleri",
            Label = "KullanÄ±cÄ± Ä°ÅŸlemleri",
            Icon = "Users",
            SortOrder = 1,
        };
        context.MenuModules.Add(yonetimKullanici);
        await context.SaveChangesAsync(ct);

        yonetimKullanici.Items = new List<MenuItem>
        {
            BuildItem(permissions, "KullanÄ±cÄ± TanÄ±mlama",                  "/kullanici-yonetimi",                   "kullanici-tanimlama",                 1, "menu.kullanici-tanimlama.view"),
            // "Personel Listesi" buradan kaldÄ±rÄ±ldÄ± â€” yeni "Personel Ä°ÅŸlemleri" top-level modÃ¼lÃ¼ne taÅŸÄ±ndÄ± (SortOrder 14).
            BuildItem(permissions, "KullanÄ±cÄ± Yetki GruplarÄ±",             "/kullanici-yetki-gruplari",             "kullanici-yetki-gruplari",            2, "menu.kullanici-yetki-gruplari.view"),
            BuildItem(permissions, "KullanÄ±cÄ± Rapor GruplarÄ±",             "/kullanici-rapor-gruplari",             "kullanici-rapor-gruplari",            3, "menu.kullanici-rapor-gruplari.view"),
            BuildItem(permissions, "KullanÄ±cÄ± GruplarÄ± SÄ±nÄ±rlandÄ±rmalar",  "/kullanici-grup-sinirlandirmalar",      "kullanici-grup-sinirlandirmalar",     4, "menu.kullanici-grup-sinirlandirmalar.view"),
            BuildItem(permissions, "KullanÄ±cÄ± Rapor TasarÄ±m GruplarÄ±",     "/kullanici-rapor-tasarim-gruplari",     "kullanici-rapor-tasarim-gruplari",    5, "menu.kullanici-rapor-tasarim-gruplari.view"),
            BuildItem(permissions, "KullanÄ±cÄ± Vekalet Ä°ÅŸlemleri",          "/kullanici-vekalet-islemleri",          "kullanici-vekalet-islemleri",         6, "menu.kullanici-vekalet-islemleri.view"),
            BuildItem(permissions, "LOG",                                  "/denetim-kayitlari",                    "audit-log",                           7, "menu.audit-log.view"),
        };

        // â”€â”€â”€ ModÃ¼l 12.2: YÃ¶netim â†’ Genel Ayarlar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var yonetimGenel = new MenuModule
        {
            ParentId = yonetim.Id,
            Slug = "yonetim-genel-ayarlar",
            Label = "Genel Ayarlar",
            Icon = "Settings",
            SortOrder = 2,
        };
        context.MenuModules.Add(yonetimGenel);
        await context.SaveChangesAsync(ct);

        yonetimGenel.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ãœniversite Bilgileri",                 "/universite-bilgileri",                 "universite-bilgileri",                1, "menu.universite-bilgileri.view"),
            BuildItem(permissions, "Kurumsal Entegrasyon TanÄ±mlarÄ±",       "/kurumsal-entegrasyon-tanimlari",       "kurumsal-entegrasyon-tanimlari",      2, "menu.kurumsal-entegrasyon-tanimlari.view"),
            BuildItem(permissions, "Belge SÄ±nÄ±rlandÄ±rmalar",               "/belge-sinirlandirmalar",               "belge-sinirlandirmalar",              3, "menu.belge-sinirlandirmalar.view"),
            BuildItem(permissions, "Web ModÃ¼l AyarlarÄ±",                   "/web-modul-ayarlari",                   "web-modul-ayarlari",                  4, "menu.web-modul-ayarlari.view"),
            BuildItem(permissions, "Sistem KonfigÃ¼rasyonu",                "/sistem-ayarlari",                      "system-config",                       5, "menu.system-config.view"),
            BuildItem(permissions, "EBYS Ä°mza Yetkilileri",                "/ebys-imza-yetkilileri",                "ebys-imza-yetkilileri",               6, "menu.ebys-imza-yetkilileri.view"),
            BuildItem(permissions, "FakÃ¼lte Ä°mza YÃ¼kleme Ä°ÅŸlemleri",       "/fakulte-imza-yukleme-islemleri",       "fakulte-imza-yukleme",                7, "menu.fakulte-imza-yukleme.view"),
        };

        // â”€â”€â”€ ModÃ¼l 14: Personel Ä°ÅŸlemleri (top-level, SystemAdmin-only) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Patch: "Personel Listesi" YÃ¶netim â†’ KullanÄ±cÄ± Ä°ÅŸlemleri'nden buraya taÅŸÄ±ndÄ±.
        // AllowedRolesCsv = "SystemAdministrator" â†’ kullanÄ±cÄ± kararÄ±: ÅŸimdilik sadece SysAdmin.
        // Ä°leride baÅŸka roller eklenecekse AllowedRolesCsv gÃ¼ncellenir + ilgili rol(ler)e
        // "menu.personel-listesi.view" RolePermission atamasÄ± yapÄ±lÄ±r.
        var personelIslemleri = new MenuModule
        {
            Slug = "personel-islemleri",
            Label = "Personel Ä°ÅŸlemleri",
            Icon = "IdCard",
            SortOrder = 14,
            AllowedRolesCsv = "SystemAdministrator",
        };
        context.MenuModules.Add(personelIslemleri);
        await context.SaveChangesAsync(ct);

        personelIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Personel Listesi", "/personel-listesi", "personel-listesi", 1, "menu.personel-listesi.view"),
        };

        // â”€â”€â”€ ModÃ¼l 15: Ã–ÄŸretim Ä°ÅŸlemleri (hoca/danÄ±ÅŸman/yÃ¶netici iÃ§in) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Patch (post-Phase 1): SystemAdmin'in gÃ¶rmemesi gereken hoca/danÄ±ÅŸman gÃ¼nlÃ¼k
        // iÅŸ sayfalarÄ± ayrÄ± bir modÃ¼lde toplandÄ±. AllowedRolesCsv SysAdmin shortcut'Ä±nÄ±
        // bypass eder: admin/Ã¶ÄŸrenci/idari personel bu modÃ¼lÃ¼ hiÃ§ gÃ¶rmez.
        var ogretimIslemleri = new MenuModule
        {
            Slug = "ogretim-islemleri",
            Label = "Ã–ÄŸretim Ä°ÅŸlemleri",
            Icon = "GraduationCap",
            SortOrder = 15,
            AllowedRolesCsv = "Instructor,AcademicAdvisor,Dean,DepartmentManager",
        };
        context.MenuModules.Add(ogretimIslemleri);
        await context.SaveChangesAsync(ct);

        ogretimIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Duyurular",        "/duyurular",                "duyurular-ogretim",          1, "menu.duyurular.view"),
            BuildItem(permissions, "Derslerim",        "/derslerim",                "derslerim",                  2, "menu.derslerim.view"),
            BuildItem(permissions, "Ders Programım",   "/ders-programim",           "ders-programim",             3, "menu.ders-programim.view"),
            BuildItem(permissions, "Yoklama Yönetimi", "/yoklama-girisi",           "yoklama-girisi",             4, "menu.yoklama-girisi.view"),
            BuildItem(permissions, "Not Girişi",       "/not-girisi",               "not-girisi",                 5, "menu.not-girisi.view"),
            BuildItem(permissions, "Ders Kısıtları",   "/danisman-ders-kisitlari",  "danisman-ders-kisitlari",    6, "menu.danisman-ders-kisitlari.view"),
        };

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// MenuItem fabrikasÄ± â€” null-safe permission lookup ile tek satÄ±rda item Ã¼retir.
    /// Permission key bulunamazsa MenuItemPermission boÅŸ baÄŸlanÄ±r ve uyarÄ± log'lanÄ±r.
    /// </summary>
    private static MenuItem BuildItem(
        IReadOnlyDictionary<string, Permission> permissions,
        string label,
        string route,
        string slug,
        int sortOrder,
        string permissionKey,
        bool openInNewTab = false)
    {
        var item = new MenuItem
        {
            Label = label,
            Route = route,
            Slug = slug,
            SortOrder = sortOrder,
            OpenInNewTab = openInNewTab,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (permissions.TryGetValue(permissionKey, out var perm))
        {
            item.MenuItemPermissions = new List<MenuItemPermission>
            {
                new() { PermissionId = perm.Id }
            };
        }
        else
        {
            Console.WriteLine($"[MenuStructureSeed] WARNING: Permission key '{permissionKey}' bulunamadÄ±, item '{label}' permission'sÄ±z ekleniyor.");
        }

        return item;
    }
}

