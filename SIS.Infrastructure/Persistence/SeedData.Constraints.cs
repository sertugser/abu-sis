using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedConstraintDefinitionsAsync(AppDbContext context)
    {
        if (await context.ConstraintDefinitions.AnyAsync())
            return;

        var definitions = new List<ConstraintDefinition>
        {
            // ── Hard Constraints (15) ───────────────────────────────
            new()
            {
                Code = "H3",
                Name = "Kapasite Kontrolü",
                Description = "Derslik kapasitesinin şube kontenjanını karşılayıp karşılamadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 1
            },
            new()
            {
                Code = "H4",
                Name = "Derslik Tipi Uyumu",
                Description = "Dersin gerektirdiği derslik tipinin (lab, amfi, normal) atanan derslikle uyumunu kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 2
            },
            new()
            {
                Code = "H10",
                Name = "Fakülte Derslik Ataması",
                Description = "Dersliklerin dönemlik fakülte atamasına uygun kullanılıp kullanılmadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 3
            },
            new()
            {
                Code = "H1",
                Name = "Derslik Çakışması",
                Description = "Aynı dersliğe aynı zaman diliminde birden fazla ders atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 4
            },
            new()
            {
                Code = "H2",
                Name = "Öğretim Üyesi Çakışması",
                Description = "Aynı öğretim üyesine aynı zaman diliminde birden fazla ders atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 5
            },
            new()
            {
                Code = "H5",
                Name = "Müfredat Çakışması",
                Description = "Aynı müfredattaki zorunlu derslerin aynı zaman dilimine atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 6
            },
            new()
            {
                Code = "H6",
                Name = "Ders Bileşen Çakışması",
                Description = "Aynı dersin teorik ve uygulama bileşenlerinin çakışmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 7
            },
            new()
            {
                Code = "H8",
                Name = "Aynı Ders Şube Çakışması",
                Description = "Aynı dersin farklı şubelerinin aynı zaman dilimine atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 8
            },
            new()
            {
                Code = "H-WL",
                Name = "Haftalık Yük Limiti",
                Description = "Öğretim üyesinin haftalık ders yükünün YÖK unvanına göre belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 9
            },
            new()
            {
                Code = "H-DL",
                Name = "Günlük Yük Limiti",
                Description = "Öğretim üyesinin günlük ders yükünün belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxDailyHours": 8}""",
                SortOrder = 10
            },
            new()
            {
                Code = "H-DO",
                Name = "Boş Gün Zorunluluğu",
                Description = "Öğretim üyesinin haftada en az bir boş gün almasını zorunlu kılar.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 11
            },
            new()
            {
                Code = "H11",
                Name = "Eşkoşul Uyumu",
                Description = "Eşkoşul (corequisite) derslerin aynı dönemde ve çakışmadan programlanmasını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 12
            },
            new()
            {
                Code = "H12",
                Name = "Kredi-Saat Uyumu",
                Description = "Dersin kredi/AKTS değerine uygun sayıda slot atanıp atanmadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 13
            },
            new()
            {
                Code = "H13",
                Name = "Öğle Arası Koruması",
                Description = "Öğle arası saatlerine ders atanmasını engeller (hard mod aktifken).",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"LunchBreakStart": "11:40", "LunchBreakEnd": "12:20"}""",
                SortOrder = 14
            },
            new()
            {
                Code = "H15",
                Name = "Ardışık Ders Limiti",
                Description = "Öğretim üyesinin aralıksız ardışık ders sayısının belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxConsecutiveHours": 4}""",
                SortOrder = 15
            },

            // ── Soft Constraints (9) ────────────────────────────────
            new()
            {
                Code = "S1",
                Name = "Doluluk Oranı Optimizasyonu",
                Description = "Derslik doluluk oranının hedef aralıkta (%60-%85) olmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MinOccupancy": 60, "MaxOccupancy": 85, "Weight": 3}""",
                SortOrder = 101
            },
            new()
            {
                Code = "S2",
                Name = "Günlük Yük Dengeleme",
                Description = "Öğretim üyesinin günlük ders saatlerinin dengeli dağılmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxDailyHours": 6, "Weight": 2}""",
                SortOrder = 102
            },
            new()
            {
                Code = "S3",
                Name = "Slot Yoğunluk Kontrolü",
                Description = "Popüler zaman dilimlerinde aşırı yığılmayı önlemeye çalışır.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxSectionsPerSlot": 5, "Weight": 2}""",
                SortOrder = 103
            },
            new()
            {
                Code = "S4",
                Name = "Birlikte Alınan Ders Kontrolü",
                Description = "Farklı bölümlerden sık birlikte alınan derslerin çakışmamasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MinCoPairThreshold": 20, "Weight": 3}""",
                SortOrder = 104
            },
            new()
            {
                Code = "S5",
                Name = "Öğle Arası Tercihi",
                Description = "Öğle arası saatlerine ders atanmamasını tercih eder (soft mod).",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 2}""",
                SortOrder = 105
            },
            new()
            {
                Code = "S7",
                Name = "Öğrenci Boşluk Minimizasyonu",
                Description = "Öğrencilerin gün içindeki ders arası boşluklarını minimize etmeye çalışır.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxGapSlots": 3, "Weight": 2}""",
                SortOrder = 106
            },
            new()
            {
                Code = "S8",
                Name = "Öğretim Üyesi Gün Dağılımı",
                Description = "Öğretim üyesinin derslerinin haftaya dengeli yayılmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 1}""",
                SortOrder = 107
            },
            new()
            {
                Code = "S9",
                Name = "Erken/Geç Saat Kontrolü",
                Description = "Erken sabah ve geç akşam saatlerine ders atanmasına ceza uygular.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 1}""",
                SortOrder = 108
            },
            new()
            {
                Code = "S12",
                Name = "Hafta İçi Denge",
                Description = "Derslerin hafta içi günlere dengeli dağılmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 1}""",
                SortOrder = 109
            },
        };

        context.ConstraintDefinitions.AddRange(definitions);
        await context.SaveChangesAsync();
    }
}
