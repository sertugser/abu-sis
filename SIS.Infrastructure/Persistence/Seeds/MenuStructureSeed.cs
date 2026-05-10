using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// MenuModule + MenuItem ağacının seed'i. Idempotent (boş tabloda çalışır).
/// Patch (post-Phase 1):
///   - "Genel İşlemler" modülü öğrenci-merkezli: Akademik Takvim, Duyurular, Alınan Dersler
///   - "Sık Kullanılanlar" sadece Ana Sayfa + frontend favori injection
///   - "Duyuru Yönetimi" modülünde admin için ayrı "Duyurular" link (route /duyurular,
///     slug "duyurular-admin", permission menu.duyurular-admin.view) — Route UNIQUE kalktı
///   - Her modüle stabil Slug eklendi (MenuModule.Slug, UNIQUE)
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

        // ─── Modül 1: Genel İşlemler (Patch 2C: SADECE Student) ──────────────────
        var genelIslemler = new MenuModule
        {
            Slug = "genel-islemler",
            Label = "Genel İşlemler",
            Icon = "Compass",
            SortOrder = 1,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(genelIslemler);
        await context.SaveChangesAsync(ct);

        genelIslemler.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Akademik Takvim",   "/akademik-takvim",     "akademik-takvim",   1, "menu.akademik-takvim.view"),
            BuildItem(permissions, "Duyurular",         "/duyurular",           "duyurular",         2, "menu.duyurular.view"),
            BuildItem(permissions, "Alınan Dersler",    "/alinan-dersler",      "alinan-dersler",    3, "menu.alinan-dersler.view"),
        };

        // ─── Modül 2: Sık Kullanılanlar (favori-odaklı, sadece Ana Sayfa default) ─
        // Frontend favorites injection (useFavorites) ek item'lar push eder.
        var sikKullanilanlar = new MenuModule
        {
            Slug = "sik-kullanilanlar",
            Label = "Sık Kullanılanlar",
            Icon = "LayoutGrid",
            SortOrder = 2,
        };
        context.MenuModules.Add(sikKullanilanlar);
        await context.SaveChangesAsync(ct);

        sikKullanilanlar.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ana Sayfa", "/", "ana-sayfa", 1, "menu.ana-sayfa.view"),
        };

        // ─── Modül 3: Akademik İşlemler (admin only) ─────────────────────────────
        var akademikIslemler = new MenuModule
        {
            Slug = "akademik-islemler",
            Label = "Akademik İşlemler",
            Icon = "Calendar",
            SortOrder = 3,
        };
        context.MenuModules.Add(akademikIslemler);
        await context.SaveChangesAsync(ct);

        akademikIslemler.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Akademik Dönem Tanımları",       "/donem-yonetimi",                "akademik-donem",              1, "menu.akademik-donem.view"),
            BuildItem(permissions, "Fakülte Tanımları",              "/fakulte-tanimlari",             "fakulte-tanimlari",           2, "menu.fakulte-tanimlari.view"),
            BuildItem(permissions, "Bölüm Tanımları",                "/bolum-tanimlari",               "bolum-tanimlari",             3, "menu.bolum-tanimlari.view"),
            BuildItem(permissions, "Program Tanımları",              "/program-tanimlari",             "program-tanimlari",           4, "menu.program-tanimlari.view"),
            BuildItem(permissions, "Akademik Takvim",                "/akademik-takvim",               "akademik-takvim-goruntuleme", 5, "menu.akademik-takvim.view"),
            BuildItem(permissions, "Takvim Oluşturucu",              "/akademik-takvim-olusturma",     "akademik-takvim-olusturma",   6, "menu.akademik-takvim-olusturma.view"),
            BuildItem(permissions, "ÖSYM Kayıt Takvimi",             "/osym-kayit-takvimi",            "osym-kayit-takvimi",          7, "menu.osym-kayit-takvimi.view"),
            BuildItem(permissions, "Program Kontenjan Bilgileri",    "/program-kontenjan",             "program-kontenjan",           8, "menu.program-kontenjan.view"),
        };

        // ─── Modül 4: Danışmanlık İşlemleri (Phase 1'de boş — Karar #6) ──────────
        var danismanlikIslemleri = new MenuModule
        {
            Slug = "danismanlik-islemleri",
            Label = "Danışmanlık İşlemleri",
            Icon = "Users",
            SortOrder = 4,
        };
        context.MenuModules.Add(danismanlikIslemleri);
        await context.SaveChangesAsync(ct);

        // ─── Modül 5: Ders ve Dönem İşlemleri (Patch 2C: SADECE Student) ────────
        var dersVeDonemIslemleri = new MenuModule
        {
            Slug = "ders-ve-donem-islemleri",
            Label = "Ders ve Dönem İşlemleri",
            Icon = "BookOpen",
            SortOrder = 5,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(dersVeDonemIslemleri);
        await context.SaveChangesAsync(ct);

        dersVeDonemIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ders Kayıt",              "/ders-kayit",            "ders-kayit",              1, "menu.ders-kayit.view"),
            BuildItem(permissions, "Akıllı Ders Önerileri",   "/akilli-planlama",       "akilli-planlama",         2, "menu.akilli-planlama.view"),
            BuildItem(permissions, "Kayıt Planım",            "/plan",                  "plan",                    3, "menu.plan.view"),
            BuildItem(permissions, "Not Listesi",             "/not-listesi",           "not-listesi",             4, "menu.not-listesi.view"),
            BuildItem(permissions, "Devamsızlık Durumu",      "/devamsizlik-durumu",    "devamsizlik-durumu",      5, "menu.devamsizlik-durumu.view"),
            BuildItem(permissions, "Dönem Ortalamaları",      "/donem-ortalamalari",    "donem-ortalamalari",      6, "menu.donem-ortalamalari.view"),
            BuildItem(permissions, "Müfredat Durum",          "/mufredat-durum",        "mufredat-durum",          7, "menu.mufredat-durum.view"),
            BuildItem(permissions, "Transkript",              "/report/transkript",     "transkript",              8, "menu.transkript.view", openInNewTab: true),
            BuildItem(permissions, "Transkript Senaryosu",    "/transkript-senaryosu",  "transkript-senaryosu",    9, "menu.transkript-senaryosu.view"),
        };

        // ─── Modül 6: Ders İşlemleri (admin + instructor + advisor) ──────────────
        var dersIslemleri = new MenuModule
        {
            Slug = "ders-islemleri",
            Label = "Ders İşlemleri",
            Icon = "BookOpen",
            SortOrder = 6,
        };
        context.MenuModules.Add(dersIslemleri);
        await context.SaveChangesAsync(ct);

        dersIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Ders Kataloğu",                  "/ders-katalogu",                       "ders-katalogu",                  1,  "menu.ders-katalogu.view"),
            BuildItem(permissions, "Müfredat İşlemleri",             "/mufredat-islemleri",                  "mufredat-islemleri",             2,  "menu.mufredat-islemleri.view"),
            BuildItem(permissions, "Müfredat Sınıf Dönem Tanımları", "/mufredat-sinif-donem-tanimlari",      "mufredat-sinif-donem",           3,  "menu.mufredat-sinif-donem.view"),
            BuildItem(permissions, "Seçmeli Ders Yapılandırması",       "/secmeli-yapilandirma",                "secmeli-yapilandirma",           4,  "menu.secmeli-yapilandirma.view"),
            BuildItem(permissions, "Ders Aç (Müfredat)",             "/ders-acma-islemleri",                 "ders-acma-islemleri",            5,  "menu.ders-acma-islemleri.view"),
            BuildItem(permissions, "Ders Aç (Hareket)",              "/donem-programi-aktarim",              "donem-programi-aktarim",         6,  "menu.donem-programi-aktarim.view"),
            BuildItem(permissions, "Şubelendirme İşlemleri",         "/subelendirme-islemleri",              "subelendirme-islemleri",         7,  "menu.subelendirme-islemleri.view"),
            BuildItem(permissions, "Ders Kontenjan İşlemleri",       "/ders-kontenjan-islemleri",            "ders-kontenjan-islemleri",       8,  "menu.ders-kontenjan-islemleri.view"),
            BuildItem(permissions, "Ders Saatleri Yönetimi",         "/ders-saatleri-yonetimi",              "ders-saatleri-yonetimi",         9,  "menu.ders-saatleri-yonetimi.view"),
            BuildItem(permissions, "Program Kısıtlamaları",          "/ders-programi-kisitlamalari",         "ders-programi-kisitlamalari",    10, "menu.ders-programi-kisitlamalari.view"),
            BuildItem(permissions, "Ders Programı Oluşturma",        "/ders-programi-olusturma",             "ders-programi-olusturma",        11, "menu.ders-programi-olusturma.view"),
            BuildItem(permissions, "Öğretim Elemanı Atama",          "/ogretim-elemani-atama",               "ogretim-elemani-atama",          12, "menu.ogretim-elemani-atama.view"),
            BuildItem(permissions, "Derslik Doluluk Tablosu",        "/derslik-doluluk-tablosu",             "derslik-doluluk",                13, "menu.derslik-doluluk.view"),
            BuildItem(permissions, "Ders Veren Öğretim Üyeleri",     "/ders-veren-ogretim-uyeleri",          "ders-veren-ogretim-uyeleri",     14, "menu.ders-veren-ogretim-uyeleri.view"),
            BuildItem(permissions, "Program Onayı",                  "/program-onayi",                       "program-onayi",                  15, "menu.program-onayi.view"),
            BuildItem(permissions, "Sınıf Listeleri",                "/sinif-listeleri",                     "sinif-listeleri",                16, "menu.sinif-listeleri.view"),
        };

        // ─── Modül 7: Bölüm İşlemleri (hoca + admin için) ────────────────────────
        // Patch 2B: Proliz tarzı hoca paneli — bölüm-level operasyonlar
        var bolumIslemleri = new MenuModule
        {
            Slug = "bolum-islemleri",
            Label = "Bölüm İşlemleri",
            Icon = "Briefcase",
            SortOrder = 7,
        };
        context.MenuModules.Add(bolumIslemleri);
        await context.SaveChangesAsync(ct);

        bolumIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Şubelendirme İşlemleri",         "/subelendirme-islemleri",         "subelendirme-islemleri-bolum",      1, "menu.subelendirme-islemleri.view"),
            BuildItem(permissions, "Ders Veren Öğretim Elemanları",  "/ders-veren-ogretim-uyeleri",     "ders-veren-ogretim-uyeleri-bolum",  2, "menu.ders-veren-ogretim-uyeleri.view"),
            BuildItem(permissions, "Toplu Danışman Atama",           "/toplu-danisman-atama",           "toplu-danisman-atama-bolum",        3, "menu.toplu-danisman-atama.view"),
        };

        // ─── Modül 8: Öğrenci İşlemleri (kayma: 7 → 8) ───────────────────────────
        var ogrenciIslemleri = new MenuModule
        {
            Slug = "ogrenci-islemleri",
            Label = "Öğrenci İşlemleri",
            Icon = "Users",
            SortOrder = 8,
        };
        context.MenuModules.Add(ogrenciIslemleri);
        await context.SaveChangesAsync(ct);

        ogrenciIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Öğrenci Rehberi",                "/ogrenci-rehberi",                     "ogrenci-rehberi",                1,  "menu.ogrenci-rehberi.view"),
            BuildItem(permissions, "Öğrenci No Değiştirme",          "/ogrenci-no-degistirme",               "ogrenci-no-degistirme",          2,  "menu.ogrenci-no-degistirme.view"),
            BuildItem(permissions, "Sınıf Atlatma İşlemleri",        "/sinif-atlatma-islemleri",             "sinif-atlatma-islemleri",        3,  "menu.sinif-atlatma-islemleri.view"),
            BuildItem(permissions, "Toplu Danışman Atama",           "/toplu-danisman-atama",                "toplu-danisman-atama",           4,  "menu.toplu-danisman-atama.view"),
            BuildItem(permissions, "Toplu Staj Danışman Atama",      "/toplu-staj-danisman-atama",           "toplu-staj-danisman-atama",      5,  "menu.toplu-staj-danisman-atama.view"),
            BuildItem(permissions, "Toplu Müfredat Atama",           "/toplu-mufredat-atama",                "toplu-mufredat-atama",           6,  "menu.toplu-mufredat-atama.view"),
            BuildItem(permissions, "Toplu Ders Kaydı",               "/toplu-ders-kaydi",                    "toplu-ders-kaydi",               7,  "menu.toplu-ders-kaydi.view"),
            BuildItem(permissions, "Ders Kayıt Düzenleme",           "/ders-kayit-duzenleme",                "ders-kayit-duzenleme",           8,  "menu.ders-kayit-duzenleme.view"),
            BuildItem(permissions, "Şubeler Arası Öğrenci Transfer", "/subeler-arasi-ogrenci-transfer",      "subeler-arasi-transfer",         9,  "menu.subeler-arasi-ogrenci-transfer.view"),
            BuildItem(permissions, "Dersler Arası Öğrenci Transfer", "/dersler-arasi-ogrenci-transfer",      "dersler-arasi-transfer",         10, "menu.dersler-arasi-ogrenci-transfer.view"),
            BuildItem(permissions, "Harf Notu Düzenleme",            "/harf-notu-duzenleme",                 "harf-notu-duzenleme",            11, "menu.harf-notu-duzenleme.view"),
        };

        // ─── Modül 9: Anketler (kayma: 8 → 9) ────────────────────────────────────
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
            BuildItem(permissions, "Ders Değerlendirme",             "/ders-degerlendirme",                  "ders-degerlendirme",             1, "menu.ders-degerlendirme.view"),
            BuildItem(permissions, "Değerlendirme Sonuçları",        "/degerlendirme-sonuclari",             "degerlendirme-sonuclari",        2, "menu.degerlendirme-sonuclari.view"),
            BuildItem(permissions, "Değerlendirme Dönemi Yönetimi",  "/degerlendirme-donemi-yonetimi",       "degerlendirme-donemi-yonetimi",  3, "menu.degerlendirme-donemi-yonetimi.view"),
        };

        // ─── Modül 10: Kaynak Yönetimi (kayma: 9 → 10) ───────────────────────────
        var kaynakYonetimi = new MenuModule
        {
            Slug = "kaynak-yonetimi",
            Label = "Kaynak Yönetimi",
            Icon = "School",
            SortOrder = 10,
        };
        context.MenuModules.Add(kaynakYonetimi);
        await context.SaveChangesAsync(ct);

        kaynakYonetimi.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Derslik Yönetimi",               "/derslik-yonetimi",                    "derslik-yonetimi",               1, "menu.derslik-yonetimi.view"),
            BuildItem(permissions, "Derslik-Fakülte Ataması",        "/derslik-fakulte-atamasi",             "derslik-fakulte-atamasi",        2, "menu.derslik-fakulte-atamasi.view"),
        };

        // ─── Modül 11: Duyuru Yönetimi (kayma: 10 → 11) — DUYURULAR LINKİ DE BURADA ─
        var duyuruYonetimi = new MenuModule
        {
            Slug = "duyuru-yonetimi-grubu",
            Label = "Duyuru Yönetimi",
            Icon = "Megaphone",
            SortOrder = 11,
        };
        context.MenuModules.Add(duyuruYonetimi);
        await context.SaveChangesAsync(ct);

        duyuruYonetimi.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Duyurular",                      "/duyurular",                           "duyurular-admin",                1, "menu.duyurular-admin.view"),
            BuildItem(permissions, "Duyuru Yönetimi",                "/duyuru-yonetimi",                     "duyuru-yonetimi",                2, "menu.duyuru-yonetimi.view"),
        };

        // ─── Modül 12: Hesabım (Patch 2C: SADECE Student) ────────────────────────
        var hesabim = new MenuModule
        {
            Slug = "hesabim",
            Label = "Hesabım",
            Icon = "User",
            SortOrder = 12,
            AllowedRolesCsv = "Student",
        };
        context.MenuModules.Add(hesabim);
        await context.SaveChangesAsync(ct);

        hesabim.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Şifre Değiştir",                 "/sifre-degistir",                      "sifre-degistir",                 1, "menu.sifre-degistir.view"),
        };

        // ─── Modül 13: Yönetim (kayma: 12 → 13, nested parent) ───────────────────
        var yonetim = new MenuModule
        {
            Slug = "yonetim",
            Label = "Yönetim",
            Icon = "Shield",
            SortOrder = 13,
        };
        context.MenuModules.Add(yonetim);
        await context.SaveChangesAsync(ct);

        // ─── Modül 12.1: Yönetim → Kullanıcı İşlemleri ───────────────────────────
        var yonetimKullanici = new MenuModule
        {
            ParentId = yonetim.Id,
            Slug = "yonetim-kullanici-islemleri",
            Label = "Kullanıcı İşlemleri",
            Icon = "Users",
            SortOrder = 1,
        };
        context.MenuModules.Add(yonetimKullanici);
        await context.SaveChangesAsync(ct);

        yonetimKullanici.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Kullanıcı Tanımlama",                  "/kullanici-yonetimi",                   "kullanici-tanimlama",                 1, "menu.kullanici-tanimlama.view"),
            // "Personel Listesi" buradan kaldırıldı — yeni "Personel İşlemleri" top-level modülüne taşındı (SortOrder 14).
            BuildItem(permissions, "Kullanıcı Yetki Grupları",             "/kullanici-yetki-gruplari",             "kullanici-yetki-gruplari",            2, "menu.kullanici-yetki-gruplari.view"),
            BuildItem(permissions, "Kullanıcı Rapor Grupları",             "/kullanici-rapor-gruplari",             "kullanici-rapor-gruplari",            3, "menu.kullanici-rapor-gruplari.view"),
            BuildItem(permissions, "Kullanıcı Grupları Sınırlandırmalar",  "/kullanici-grup-sinirlandirmalar",      "kullanici-grup-sinirlandirmalar",     4, "menu.kullanici-grup-sinirlandirmalar.view"),
            BuildItem(permissions, "Kullanıcı Rapor Tasarım Grupları",     "/kullanici-rapor-tasarim-gruplari",     "kullanici-rapor-tasarim-gruplari",    5, "menu.kullanici-rapor-tasarim-gruplari.view"),
            BuildItem(permissions, "Kullanıcı Vekalet İşlemleri",          "/kullanici-vekalet-islemleri",          "kullanici-vekalet-islemleri",         6, "menu.kullanici-vekalet-islemleri.view"),
            BuildItem(permissions, "LOG",                                  "/denetim-kayitlari",                    "audit-log",                           7, "menu.audit-log.view"),
        };

        // ─── Modül 12.2: Yönetim → Genel Ayarlar ─────────────────────────────────
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
            BuildItem(permissions, "Üniversite Bilgileri",                 "/universite-bilgileri",                 "universite-bilgileri",                1, "menu.universite-bilgileri.view"),
            BuildItem(permissions, "Kurumsal Entegrasyon Tanımları",       "/kurumsal-entegrasyon-tanimlari",       "kurumsal-entegrasyon-tanimlari",      2, "menu.kurumsal-entegrasyon-tanimlari.view"),
            BuildItem(permissions, "Belge Sınırlandırmalar",               "/belge-sinirlandirmalar",               "belge-sinirlandirmalar",              3, "menu.belge-sinirlandirmalar.view"),
            BuildItem(permissions, "Web Modül Ayarları",                   "/web-modul-ayarlari",                   "web-modul-ayarlari",                  4, "menu.web-modul-ayarlari.view"),
            BuildItem(permissions, "Sistem Konfigürasyonu",                "/sistem-ayarlari",                      "system-config",                       5, "menu.system-config.view"),
            BuildItem(permissions, "EBYS İmza Yetkilileri",                "/ebys-imza-yetkilileri",                "ebys-imza-yetkilileri",               6, "menu.ebys-imza-yetkilileri.view"),
            BuildItem(permissions, "Fakülte İmza Yükleme İşlemleri",       "/fakulte-imza-yukleme-islemleri",       "fakulte-imza-yukleme",                7, "menu.fakulte-imza-yukleme.view"),
        };

        // ─── Modül 14: Personel İşlemleri (top-level, SystemAdmin-only) ──────────
        // Patch: "Personel Listesi" Yönetim → Kullanıcı İşlemleri'nden buraya taşındı.
        // AllowedRolesCsv = "SystemAdministrator" → kullanıcı kararı: şimdilik sadece SysAdmin.
        // İleride başka roller eklenecekse AllowedRolesCsv güncellenir + ilgili rol(ler)e
        // "menu.personel-listesi.view" RolePermission ataması yapılır.
        var personelIslemleri = new MenuModule
        {
            Slug = "personel-islemleri",
            Label = "Personel İşlemleri",
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

        // ─── Modül 15: Öğretim İşlemleri (hoca/danışman/yönetici için) ───────────
        // Patch (post-Phase 1): SystemAdmin'in görmemesi gereken hoca/danışman günlük
        // iş sayfaları ayrı bir modülde toplandı. AllowedRolesCsv SysAdmin shortcut'ını
        // bypass eder: admin/öğrenci/idari personel bu modülü hiç görmez.
        var ogretimIslemleri = new MenuModule
        {
            Slug = "ogretim-islemleri",
            Label = "Öğretim İşlemleri",
            Icon = "GraduationCap",
            SortOrder = 15,
            AllowedRolesCsv = "Instructor,AcademicAdvisor,Dean,DepartmentManager",
        };
        context.MenuModules.Add(ogretimIslemleri);
        await context.SaveChangesAsync(ct);

        ogretimIslemleri.Items = new List<MenuItem>
        {
            BuildItem(permissions, "Derslerim",       "/derslerim",                "derslerim",                  1, "menu.derslerim.view"),
            BuildItem(permissions, "Ders Programım",  "/ders-programim",           "ders-programim",             2, "menu.ders-programim.view"),
            BuildItem(permissions, "Yoklama Yönetimi", "/yoklama-girisi",          "yoklama-girisi",             3, "menu.yoklama-girisi.view"),
            BuildItem(permissions, "Not Girişi",      "/not-girisi",               "not-girisi",                 4, "menu.not-girisi.view"),
            BuildItem(permissions, "Ders Kısıtları",  "/danisman-ders-kisitlari",  "danisman-ders-kisitlari",    5, "menu.danisman-ders-kisitlari.view"),
        };

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// MenuItem fabrikası — null-safe permission lookup ile tek satırda item üretir.
    /// Permission key bulunamazsa MenuItemPermission boş bağlanır ve uyarı log'lanır.
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
            Console.WriteLine($"[MenuStructureSeed] WARNING: Permission key '{permissionKey}' bulunamadı, item '{label}' permission'sız ekleniyor.");
        }

        return item;
    }
}
