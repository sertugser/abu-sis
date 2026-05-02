using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    /// <summary>
    /// Otomatik programlama algoritmasını etkileyen tüm scheduling ayarları.
    /// /ders-programi-kisitlamalari sayfasındaki "Algoritma Parametreleri" sekmesinden yönetilir.
    /// </summary>
    public static async Task SeedSchedulingAlgorithmConfigAsync(AppDbContext context)
    {
        var configs = new List<SchedulingAlgorithmConfig>
        {
            // ── Hard Kısıt Eşikleri ──────────────────────────────
            new() { Key = "Classroom.AmphiThreshold", Value = "100", Category = "HardThreshold", DataType = "int", Description = "Bir dersin kontenjanı bu sayıyı geçerse amfi zorunlu olur. (Derslik Tipi Uyumu kısıtı için kullanılır.)" },
            new() { Key = "Schedule.LunchBreak.IsHard", Value = "true", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: öğle arasına ders koyulamaz (zorunlu). KAPALI: koyulabilir ama ceza alır (tercihli)." },
            new() { Key = "Schedule.LunchBreakStart", Value = "11:40", Category = "HardThreshold", DataType = "string", Description = "Öğle arasının başlangıç saati (HH:mm). Bu pencereye değen dersler 'öğle arası ihlali' sayılır." },
            new() { Key = "Schedule.LunchBreakEnd", Value = "12:20", Category = "HardThreshold", DataType = "string", Description = "Öğle arasının bitiş saati (HH:mm)." },
            new() { Key = "Schedule.MaxConsecutiveHours", Value = "4", Category = "HardThreshold", DataType = "int", Description = "Bir öğretim üyesi en fazla kaç saat aralıksız ders verebilir. Bu sayıyı geçen blok zorunlu olarak bölünür." },

            // ── Soft Kısıt Eşikleri ──────────────────────────────
            new() { Key = "Classroom.OccupancyTarget.Min", Value = "60", Category = "SoftThreshold", DataType = "int", Description = "Bir derslik en az yüzde kaç dolu olmalı (S1 hedefi). Daha düşük doluluk soft penalty alır." },
            new() { Key = "Classroom.OccupancyTarget.Max", Value = "85", Category = "SoftThreshold", DataType = "int", Description = "Bir derslik en fazla yüzde kaç dolu olmalı (S1 hedefi). Aşırı dolu sınıflar da penalty alır." },
            new() { Key = "SoftConstraint.S2.MaxDailyHours", Value = "6", Category = "SoftThreshold", DataType = "int", Description = "Bir öğretim üyesinin günde kaç saatten fazla ders almaması ideal. Aşılan her saat penalty üretir." },
            new() { Key = "SoftConstraint.S3.MaxSectionsPerSlot", Value = "5", Category = "SoftThreshold", DataType = "int", Description = "Aynı saat diliminde en fazla kaç şube olabilir (prime-time yığılması). Aşılan her şube penalty alır." },
            new() { Key = "SoftConstraint.S4.MinCoPairThreshold", Value = "20", Category = "SoftThreshold", DataType = "int", Description = "Geçmiş kayıtlara göre kaç öğrenciyi etkileyen ders çiftleri 'sık birlikte alınan' sayılır. Eşik altında çakışma penalty üretmez." },
            new() { Key = "SoftConstraint.S7.MaxGapSlots", Value = "3", Category = "SoftThreshold", DataType = "int", Description = "Öğrencinin gün içinde kaç ardışık boş slota toleransı var. Aşan her boşluk penalty üretir." },

            // ── Simulated Annealing Parametreleri ────────────────
            new() { Key = "Schedule.SA.MaxIterations", Value = "10000", Category = "SA", DataType = "int", Description = "Motor en fazla kaç iyileştirme denemesi yapsın. Artırırsan daha iyi program, ama daha yavaş çalışır. Öneri: 10000." },
            new() { Key = "Schedule.SA.InitialTemperature", Value = "100", Category = "SA", DataType = "int", Description = "Motorun başlangıçta ne kadar cesur olacağı (geniş arama). Zamanla soğur ve daha titiz olur. Öneri: 100." },
            new() { Key = "Schedule.SA.TimeoutSeconds", Value = "90", Category = "SA", DataType = "int", Description = "Motor en fazla kaç saniye çalışsın. Süre dolunca elindeki en iyi programı verir. Öneri: 90." },
            new() { Key = "Schedule.SA.StagnationLimit", Value = "2000", Category = "SA", DataType = "int", Description = "Motor kaç deneme üst üste iyileşme sağlamazsa sıkışmış sayılıp durur. Öneri: 2000." },
            new() { Key = "Schedule.SA.BacktrackLimit", Value = "50", Category = "SA", DataType = "int", Description = "İlk program kurulurken (Construction Solver) kaç kez geri alıp tekrar denenebilir. Öneri: 50." },
            new() { Key = "Schedule.SA.RelocateWeight", Value = "50", Category = "SA", DataType = "int", Description = "Motor 'tek bir dersin saatini değiştir' hamlesini ne sıklıkla denesin. Üç hamle ağırlığının toplamı 100 olur (varsayılan: 50/35/15)." },
            new() { Key = "Schedule.SA.SwapWeight", Value = "35", Category = "SA", DataType = "int", Description = "Motor 'iki dersin yerini değiştir' hamlesini ne sıklıkla denesin." },
            new() { Key = "Schedule.SA.ChainWeight", Value = "15", Category = "SA", DataType = "int", Description = "Motor 'zincirleme yer değiştir' (3+ ders aynı anda kaydır) hamlesini ne sıklıkla denesin." },

            // ── LNS (Large Neighborhood Search) Parametreleri ────
            new() { Key = "Schedule.LNS.MaxIterations", Value = "100", Category = "LNS", DataType = "int", Description = "Yan motor (yerel optimum kaçışı) en fazla kaç deneme yapsın. Ana motor sıkıştığında devreye girer." },
            new() { Key = "Schedule.LNS.TimeoutSeconds", Value = "30", Category = "LNS", DataType = "int", Description = "Yan motor en fazla kaç saniye çalışsın." },
            new() { Key = "Schedule.LNS.DestroySize", Value = "8", Category = "LNS", DataType = "int", Description = "Yan motor sıkışınca kaç dersin programını yıkıp baştan çözsün. Büyütürsen daha geniş kurtarma, daha yavaş." },

            // ── MRV (Minimum Remaining Values) Heuristic ─────────
            new() { Key = "Schedule.MRV.ClassroomWeight", Value = "3", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: derslik bulması zor olan dersleri ne kadar öne al. Yüksek değer = uygun derslik az olan dersler önce." },
            new() { Key = "Schedule.MRV.SlotWeight", Value = "2", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: zaman dilimi seçeneği az olan dersleri ne kadar öne al." },
            new() { Key = "Schedule.MRV.LabWeight", Value = "2", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: lab gerektiren dersleri ne kadar öne al (uygun lab sınırlı olabilir)." },
            new() { Key = "Schedule.MRV.CapacityWeight", Value = "1", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: kontenjanı yüksek dersleri ne kadar öne al (büyük dersliğe sığması zor)." },

            // ── Diğer Algoritma Ayarları ─────────────────────────
            new() { Key = "Scheduling.MaxBacktrackingDepth", Value = "3", Category = "Other", DataType = "int", Description = "Motor takıldığında kaç adım geri gidip tekrar denesin. Yüksek = daha titiz, daha yavaş. Öneri: 3." },
            new() { Key = "Schedule.TheoryLabSplitThreshold", Value = "4", Category = "Other", DataType = "int", Description = "Bir dersin teori + lab toplamı bu saati aşarsa motor otomatik olarak farklı günlere böler. Öneri: 4 saat." },
            new() { Key = "Schedule.SplittableBlockTypes", Value = "Lab", Category = "Other", DataType = "string", Description = "Bir derslik yetmediğinde hangi tip dersler aynı saatte iki ayrı dersliğe bölünebilir. Virgülle ayrılmış (örn: 'Theory,Lab'). Boş = bölünme yok." },
        };

        var existingKeys = await context.SchedulingAlgorithmConfigs
            .AsNoTracking()
            .Select(c => c.Key)
            .ToListAsync();
        var existingKeySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingConfigs = configs
            .Where(c => !existingKeySet.Contains(c.Key))
            .ToList();

        if (missingConfigs.Count == 0)
            return;

        context.SchedulingAlgorithmConfigs.AddRange(missingConfigs);
        await context.SaveChangesAsync();
    }
}
