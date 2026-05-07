using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedSystemConfigAsync(AppDbContext context)
    {
        var configs = new List<SystemConfig>
        {
            new() { Key = "Attendance.TermWeekCount", Value = "14", Category = "Attendance", DataType = "int", Description = "Devamsizlik hesabinda kullanilan aktif donem hafta sayisi" },
            new() { Key = "Attendance.TheoryAbsenceLimitPercent", Value = "30", Category = "Attendance", DataType = "decimal", Description = "Teorik derslerde devamsizliktan kalma esigi (%)" },
            new() { Key = "Attendance.LabAbsenceLimitPercent", Value = "20", Category = "Attendance", DataType = "decimal", Description = "Uygulama/lab derslerinde devamsizliktan kalma esigi (%)" },

            // ── Kayıt ECTS Limitleri ─────────────────────────────
            new() { Key = "Registration.EctsLimit.CgpaBelow200", Value = "30", Category = "Registration", DataType = "int", Description = "CGPA < 2.00 olan öğrencilerin alabileceği max AKTS" },
            new() { Key = "Registration.EctsLimit.Cgpa200To249", Value = "42", Category = "Registration", DataType = "int", Description = "CGPA 2.00-2.49 arası öğrencilerin alabileceği max AKTS" },
            new() { Key = "Registration.EctsLimit.CgpaAbove250", Value = "48", Category = "Registration", DataType = "int", Description = "CGPA >= 2.50 olan öğrencilerin alabileceği max AKTS" },

            // ── GPA Eşikleri ──────────────────────────────────────
            new() { Key = "GPA.RetentionThreshold", Value = "1.70", Category = "GPA", DataType = "decimal", Description = "Bu CGPA altındaki öğrenciler sınıfta kalır (1. dönem sonrası)" },
            new() { Key = "GPA.MaxScale", Value = "4.00", Category = "GPA", DataType = "decimal", Description = "GPA üst sınırı (4.00 = 4'lük sistem, 100.00 = 100'lük sistem). Form input cap'i ve validation için kullanılır." },

            // ── Denetim Kayıtları ─────────────────────────────────
            new() { Key = "Audit.RetentionDays", Value = "365", Category = "General", DataType = "int", Description = "Denetim kayıtlarının saklanma süresi (gün)" },

            // ── Global Güvenlik ve Dil ────────────────────────────
            new() { Key = "Security.PasswordRotationEnabled", Value = "true", Category = "Security", DataType = "bool", Description = "Kullanıcı şifreleri periyodik olarak değiştirmeye zorlanır mı?" },
            new() { Key = "Security.PasswordRotationMonths", Value = "6", Category = "Security", DataType = "int", Description = "Şifre değiştirme periyodu (ay)" },
            new() { Key = "Localization.DefaultLanguage", Value = "tr-TR", Category = "Localization", DataType = "string", Description = "Sistem varsayılan arayüz dili" },

            // ── Ders Çekilme Limitleri ────────────────────────────
            new() { Key = "Withdrawal.MaxCount.Undergraduate", Value = "6", Category = "Registration", DataType = "int", Description = "Lisans öğrencileri için toplam çekilme hakkı" },
            new() { Key = "Withdrawal.MaxCount.Associate", Value = "3", Category = "Registration", DataType = "int", Description = "Önlisans öğrencileri için toplam çekilme hakkı" },

            // ── Ders Programı Grid Parametreleri ─────────────────
            // Not: Otomatik programlama algoritmasını etkileyen tüm key'ler
            // ayrı bir tablo olan SchedulingAlgorithmConfigs'a taşındı (bkz. SeedData.SchedulingAlgorithmConfig).
            // SystemConfig burada sadece TimeSlot grid'i + diğer domain ayarlarını barındırır.
            new() { Key = "Schedule.SlotDurationMinutes", Value = "50", Category = "Schedule", DataType = "int", Description = "Bir ders slotunun süresi (dakika)" },
            new() { Key = "Schedule.BreakBetweenSlotsMinutes", Value = "5", Category = "Schedule", DataType = "int", Description = "İki ders arası mola süresi (dakika)" },
            new() { Key = "Schedule.MorningStartTime", Value = "09:00", Category = "Schedule", DataType = "string", Description = "Sabah ilk ders başlangıç saati" },
            new() { Key = "Schedule.AfternoonEndTime", Value = "18:30", Category = "Schedule", DataType = "string", Description = "Öğleden sonra son dersin en geç bitiş saati" },
            new() { Key = "Schedule.EveningStartTime", Value = "20:10", Category = "Schedule", DataType = "string", Description = "Akşam dersleri başlangıç saati" },
            new() { Key = "Schedule.EveningSlotCount", Value = "2", Category = "Schedule", DataType = "int", Description = "Akşam ders slot sayısı" },
            new() { Key = "Schedule.WorkDays", Value = "Monday,Tuesday,Wednesday,Thursday,Friday", Category = "Schedule", DataType = "string", Description = "Ders yapılan günler (virgülle ayrılmış)" },
            new() { Key = "Schedule.FaceToFaceCutoffTime", Value = "18:00", Category = "Schedule", DataType = "string", Description = "Bu saatten sonraki TimeSlot'lar yeni oluşturulduğunda OnlineOnly damgalanır (ve FaceToFaceSlotRestriction evaluator'ı bu saatten sonrasında yüz-yüze ders kabul etmez)" },
            new() { Key = "Schedule.WeekendOnlineOnly", Value = "true", Category = "Schedule", DataType = "bool", Description = "Hafta sonu (Cts/Paz) slotları OnlineOnly damgalanır ve yüz-yüze ders kabul etmez" },

            // ── Ders Açma / Aktarım Varsayılanları ───────────────
            new() { Key = "CourseOpening.DefaultCapacityPerSection", Value = "40", Category = "CourseOpening", DataType = "int", Description = "Ders açılırken / aktarımda fallback şube başı kontenjan" },
            new() { Key = "CourseOpening.MaxSectionsPerCourse",       Value = "20", Category = "CourseOpening", DataType = "int", Description = "Bir ders için tek seferde açılabilecek max şube sayısı" },

            // ── OR-Tools CP-SAT Engine (v2) ──────────────────────
            new() { Key = "Schedule.Engine", Value = "v1-mrv", Category = "Schedule", DataType = "string", Description = "Otomatik program çekirdeği: 'v1-mrv' (mevcut MRV+SA+LNS) veya 'v2-ortools' (Google OR-Tools CP-SAT). Default v1, admin manuel v2'ye çekebilir." },
            new() { Key = "Schedule.OrTools.TimeLimitSeconds", Value = "30", Category = "Schedule", DataType = "int", Description = "CP-SAT max çözüm süresi (saniye). Bu süre dolduğunda solver son bulduğu çözümü döner." },
            new() { Key = "Schedule.OrTools.NumWorkers", Value = "8", Category = "Schedule", DataType = "int", Description = "CP-SAT parallel portfolio search worker sayısı. CPU çekirdek sayınızdan fazla yapmak fayda vermez." },
            new() { Key = "Schedule.OrTools.RandomSeed", Value = "42", Category = "Schedule", DataType = "int", Description = "Determinizm için CP-SAT random seed. Aynı seed = aynı çözüm." },
            new() { Key = "Schedule.OrTools.LogToConsole", Value = "false", Category = "Schedule", DataType = "bool", Description = "CP-SAT search progress'i console'a yaz (geliştirme sırasında true)." },
            new() { Key = "Schedule.OrTools.SearchStrategy", Value = "auto", Category = "Schedule", DataType = "string", Description = "CP-SAT search strategy: 'auto', 'fixed', 'portfolio'." },
            new() { Key = "Schedule.OrTools.UnsatCoreEnabled", Value = "true", Category = "Schedule", DataType = "bool", Description = "Infeasible çözümde UNSAT core extraction yap. Performance penalty küçük, default true." },
        };

        var existingKeys = await context.SystemConfigs
            .AsNoTracking()
            .Select(config => config.Key)
            .ToListAsync();
        var existingKeySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingConfigs = configs
            .Where(config => !existingKeySet.Contains(config.Key))
            .ToList();

        if (missingConfigs.Count == 0)
            return;

        context.SystemConfigs.AddRange(missingConfigs);
        await context.SaveChangesAsync();
    }
}
