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
            // Not: Kısıda özel eşikler (H4 AmphiThreshold, H13 LunchBreakStart/End,
            // H15 MaxConsecutiveHours, H16 TheoryLabSplitThreshold) ConstraintDefinition.Parameters
            // JSON'una taşındı — admin ilgili kısıt kartından düzenliyor (tek otorite).
            new() { Key = "Schedule.LunchBreak.IsHard", Value = "true", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: öğle arasına ders koyulamaz (zorunlu). KAPALI: koyulabilir ama ceza alır (tercihli)." },

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
            new() { Key = "Schedule.SplittableBlockTypes", Value = "Lab", Category = "Other", DataType = "string", Description = "Bir derslik yetmediğinde hangi tip dersler aynı saatte iki ayrı dersliğe bölünebilir. Virgülle ayrılmış (örn: 'Theory,Lab'). Boş = bölünme yok." },

            // Faz 1.5: Construction Solver'ın ilk programı kurarken her ders için kaç aday yeri
            // soft penalty ile puanlayıp en iyisini seçeceği. 1 = eski 'ilk feasible' davranışı (geriye uyumluluk).
            // 5-10 = en iyi öneri. Yüksek = daha iyi başlangıç, biraz daha yavaş.
            new() { Key = "Schedule.Construction.SoftLookaheadCount", Value = "10", Category = "Other", DataType = "int", Description = "İlk programı kurarken her ders için kaç aday yer puanlansın. 1 = ilk uygunla yetin (eski davranış). 5-10 = en az ceza vereni seç (önerilen)." },

            // Faz 2: aday üretici (CandidateGenerator) sınırları — eski hardcoded sabitler config'e taşındı.
            // Defaults büyütüldü ki paralel split ve büyük dersliklerin tamamı görünebilsin.
            new() { Key = "Schedule.Candidate.MaxCombinations", Value = "500", Category = "Other", DataType = "int", Description = "Bir ders için en fazla kaç saat/derslik kombinasyonu test edilsin. Yüksek = daha geniş arama, daha yavaş. Eski default: 200." },
            new() { Key = "Schedule.Candidate.MaxWindowsPerDay", Value = "10", Category = "Other", DataType = "int", Description = "Bir derslik bir günde en fazla kaç saat aralığı önersin. Düşük = hızlı, ama bazı uygun saatler atlanır. Eski default: 5." },
            new() { Key = "Schedule.Candidate.MaxClassroomsPerType", Value = "25", Category = "Other", DataType = "int", Description = "Bir ders için en fazla kaç farklı derslik aday olarak değerlendirilsin. Eski default: 10 (14 dersliği olan fakültede 4'ü dışlıyordu)." },
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
