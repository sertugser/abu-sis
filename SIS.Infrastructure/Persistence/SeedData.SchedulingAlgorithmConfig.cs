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
            new() { Key = "Classroom.AmphiThreshold", Value = "100", Category = "HardThreshold", DataType = "int", Description = "Bu sayıdan fazla kontenjan → Amfi zorunlu (H4)" },
            new() { Key = "Schedule.LunchBreak.IsHard", Value = "true", Category = "HardThreshold", DataType = "bool", Description = "Öğle arası hard constraint mi (H13) soft constraint mi (S5)" },
            new() { Key = "Schedule.MaxConsecutiveHours", Value = "4", Category = "HardThreshold", DataType = "int", Description = "Hocanın aralıksız max ardışık ders saati (H15)" },

            // ── Soft Kısıt Eşikleri ──────────────────────────────
            new() { Key = "Classroom.OccupancyTarget.Min", Value = "60", Category = "SoftThreshold", DataType = "int", Description = "S1: Derslik doluluk oranı alt hedefi (%)" },
            new() { Key = "Classroom.OccupancyTarget.Max", Value = "85", Category = "SoftThreshold", DataType = "int", Description = "S1: Derslik doluluk oranı üst hedefi (%)" },
            new() { Key = "SoftConstraint.S2.MaxDailyHours", Value = "6", Category = "SoftThreshold", DataType = "int", Description = "S2: Bir hocanın günlük max ders saati" },
            new() { Key = "SoftConstraint.S3.MaxSectionsPerSlot", Value = "5", Category = "SoftThreshold", DataType = "int", Description = "S3: Bir slot'ta max section sayısı (prime-time yığılma önleme)" },
            new() { Key = "SoftConstraint.S4.MinCoPairThreshold", Value = "20", Category = "SoftThreshold", DataType = "int", Description = "S4: Birlikte alınan ders çifti minimum öğrenci eşiği" },
            new() { Key = "SoftConstraint.S7.MaxGapSlots", Value = "3", Category = "SoftThreshold", DataType = "int", Description = "S7: Max ardışık boş slot sayısı" },

            // ── Simulated Annealing Parametreleri ────────────────
            new() { Key = "Schedule.SA.MaxIterations", Value = "10000", Category = "SA", DataType = "int", Description = "SA algoritması max iterasyon sayısı" },
            new() { Key = "Schedule.SA.InitialTemperature", Value = "100", Category = "SA", DataType = "int", Description = "SA başlangıç sıcaklığı" },
            new() { Key = "Schedule.SA.TimeoutSeconds", Value = "90", Category = "SA", DataType = "int", Description = "SA zaman aşımı (saniye)" },
            new() { Key = "Schedule.SA.StagnationLimit", Value = "2000", Category = "SA", DataType = "int", Description = "SA durgunluk limiti (iyileşme olmayan iterasyon)" },
            new() { Key = "Schedule.SA.BacktrackLimit", Value = "50", Category = "SA", DataType = "int", Description = "Construction solver backtrack limiti" },
            new() { Key = "Schedule.SA.RelocateWeight", Value = "50", Category = "SA", DataType = "int", Description = "SA relocate hamlesi ağırlığı" },
            new() { Key = "Schedule.SA.SwapWeight", Value = "35", Category = "SA", DataType = "int", Description = "SA swap hamlesi ağırlığı" },
            new() { Key = "Schedule.SA.ChainWeight", Value = "15", Category = "SA", DataType = "int", Description = "SA chain-relocate hamlesi ağırlığı" },

            // ── LNS (Large Neighborhood Search) Parametreleri ────
            new() { Key = "Schedule.LNS.MaxIterations", Value = "100", Category = "LNS", DataType = "int", Description = "LNS algoritması max iterasyon sayısı" },
            new() { Key = "Schedule.LNS.TimeoutSeconds", Value = "30", Category = "LNS", DataType = "int", Description = "LNS zaman aşımı (saniye)" },
            new() { Key = "Schedule.LNS.DestroySize", Value = "8", Category = "LNS", DataType = "int", Description = "LNS bir iterasyonda yıkılan section sayısı" },

            // ── MRV (Minimum Remaining Values) Heuristic ─────────
            new() { Key = "Schedule.MRV.ClassroomWeight", Value = "3", Category = "MRV", DataType = "int", Description = "MRV: derslik kısıtı ağırlığı" },
            new() { Key = "Schedule.MRV.SlotWeight", Value = "2", Category = "MRV", DataType = "int", Description = "MRV: zaman dilimi kısıtı ağırlığı" },
            new() { Key = "Schedule.MRV.LabWeight", Value = "2", Category = "MRV", DataType = "int", Description = "MRV: lab gereksinimi ağırlığı" },
            new() { Key = "Schedule.MRV.CapacityWeight", Value = "1", Category = "MRV", DataType = "int", Description = "MRV: kapasite kısıtı ağırlığı" },

            // ── Diğer Algoritma Ayarları ─────────────────────────
            new() { Key = "Scheduling.MaxBacktrackingDepth", Value = "3", Category = "Other", DataType = "int", Description = "Backtracking max derinlik" },
            new() { Key = "Schedule.TheoryLabSplitThreshold", Value = "4", Category = "Other", DataType = "int", Description = "Teori+lab toplamı bu değeri aşarsa ayrı günlere bölünür" },
            new() { Key = "Schedule.SplittableBlockTypes", Value = "Lab", Category = "Other", DataType = "string", Description = "Kapasite yetmeyince paralel oda split'i yapılabilen block tipleri (virgülle ayrılmış: Theory, Lab)" },
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
