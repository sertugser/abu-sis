using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    /// <summary>
    /// Algoritma parametreleri için grup metadatası — UI bu tablodan başlık/renk/sıralama
    /// okuyup gruplandırma yapıyor. Her satır SchedulingAlgorithmConfig.Category ile eşleşiyor.
    /// </summary>
    public static async Task SeedSchedulingAlgorithmConfigGroupAsync(AppDbContext context)
    {
        var groups = new List<SchedulingAlgorithmConfigGroup>
        {
            new()
            {
                Category = "HardThreshold",
                DisplayName = "Paylaşılan Kısıt Eşikleri",
                Description = "Birden fazla kısıdı etkileyen ortak eşikler (H4, H13, H15). Kısıda özel eşikler ilgili kısıt kartından düzenleniyor.",
                BadgeClass = "bg-orange-100 text-orange-800 border-orange-200",
                SortOrder = 1,
            },
            new()
            {
                Category = "SA",
                DisplayName = "Ana Optimizasyon Motoru",
                Description = "Programı iyileştiren ana motor — kaç deneme yapsın, ne kadar süre çalışsın, hangi hamlelere ağırlık versin.",
                BadgeClass = "bg-blue-100 text-blue-800 border-blue-200",
                SortOrder = 2,
            },
            new()
            {
                Category = "MRV",
                DisplayName = "Çözüm Önceliği (Başlangıç)",
                Description = "İlk programı kurarken hangi tür kısıtların öncelikli olduğunu belirler — uygun derslik az olan dersler önce, vb.",
                BadgeClass = "bg-indigo-100 text-indigo-800 border-indigo-200",
                SortOrder = 3,
            },
            new()
            {
                Category = "LNS",
                DisplayName = "Sıkışma Çözümü (Yedek Motor)",
                Description = "Ana motor takıldığında devreye giren yan motor — bir kerede kaç dersi yıkıp yeniden çözer.",
                BadgeClass = "bg-emerald-100 text-emerald-800 border-emerald-200",
                SortOrder = 4,
            },
            new()
            {
                Category = "Other",
                DisplayName = "Diğer Algoritma Ayarları",
                Description = "Genel scheduling motoru ayarları.",
                BadgeClass = "bg-slate-100 text-slate-800 border-slate-200",
                SortOrder = 5,
            },
        };

        var existing = await context.SchedulingAlgorithmConfigGroups
            .AsNoTracking()
            .Select(g => g.Category)
            .ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = groups.Where(g => !existingSet.Contains(g.Category)).ToList();

        if (missing.Count == 0)
            return;

        context.SchedulingAlgorithmConfigGroups.AddRange(missing);
        await context.SaveChangesAsync();
    }
}
