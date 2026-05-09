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
            // 2026-05-09: Eski SchedulingAlgorithmConfigs tablosu SystemConfigs'e merge edildi.
            // Solver SystemConfigs'i okuduğu için admin "Algoritma Parametreleri" panel'inden
            // değişiklik yaptığında değer anında etkili olur (eski dual-table desync düzeltildi).
            new() { Key = "Schedule.SlotDurationMinutes", Value = "50", Category = "Schedule", DataType = "int", Description = "Bir ders slotunun süresi (dakika)" },
            new() { Key = "Schedule.BreakBetweenSlotsMinutes", Value = "5", Category = "Schedule", DataType = "int", Description = "İki ders arası mola süresi (dakika)" },
            new() { Key = "Schedule.MorningStartTime", Value = "09:00", Category = "Schedule", DataType = "string", Description = "Sabah ilk ders başlangıç saati" },
            new() { Key = "Schedule.AfternoonEndTime", Value = "18:30", Category = "Schedule", DataType = "string", Description = "Öğleden sonra son dersin en geç bitiş saati" },
            new() { Key = "Schedule.EveningStartTime", Value = "20:10", Category = "Schedule", DataType = "string", Description = "Akşam dersleri başlangıç saati" },
            new() { Key = "Schedule.EveningSlotCount", Value = "2", Category = "Schedule", DataType = "int", Description = "Akşam ders slot sayısı" },
            new() { Key = "Schedule.WorkDays", Value = "Monday,Tuesday,Wednesday,Thursday,Friday", Category = "Schedule", DataType = "string", Description = "Ders yapılan günler (virgülle ayrılmış)" },
            new() { Key = "Schedule.FaceToFaceCutoffTime", Value = "18:00", Category = "Schedule", DataType = "string", Description = "Bu saatten sonraki TimeSlot'lar yeni oluşturulduğunda OnlineOnly damgalanır (ve FaceToFaceSlotRestriction evaluator'ı bu saatten sonrasında yüz-yüze ders kabul etmez)" },
            new() { Key = "Schedule.WeekendOnlineOnly", Value = "true", Category = "Schedule", DataType = "bool", Description = "Hafta sonu (Cts/Paz) slotları OnlineOnly damgalanır ve yüz-yüze ders kabul etmez" },
            new() { Key = "Schedule.LunchBreakStart", Value = "11:40", Category = "Schedule", DataType = "string", Description = "Öğle arası başlangıç saati (HH:mm). Bu saat aralığında hiçbir ders başlayamaz." },
            new() { Key = "Schedule.LunchBreakEnd", Value = "12:20", Category = "Schedule", DataType = "string", Description = "Öğle arası bitiş saati (HH:mm)." },
            new() { Key = "Schedule.MaxConsecutiveHoursPerDay", Value = "4", Category = "Schedule", DataType = "int", Description = "Hoca aynı gün içinde aralıksız bu saatten fazla ders veremez (H15 encoder, Plan 5'te real impl)." },
            new() { Key = "Schedule.DefaultInstructorMaxWeeklyHours", Value = "30", Category = "Schedule", DataType = "int", Description = "Hoca başına default haftalık maks ders saati (H-WL encoder fallback). Instructor.MaxWeeklyHours > 0 ile override edilebilir. Plan 5." },
            new() { Key = "Schedule.MaxDailyHoursPerInstructor", Value = "9", Category = "Schedule", DataType = "int", Description = "Hoca için günlük maks ders saati (H-DL encoder, Plan 5)." },

            // ── Ders Açma / Aktarım Varsayılanları ───────────────
            new() { Key = "CourseOpening.DefaultCapacityPerSection", Value = "40", Category = "CourseOpening", DataType = "int", Description = "Ders açılırken / aktarımda fallback şube başı kontenjan" },
            new() { Key = "CourseOpening.MaxSectionsPerCourse",       Value = "20", Category = "CourseOpening", DataType = "int", Description = "Bir ders için tek seferde açılabilecek max şube sayısı" },

            // ── OR-Tools CP-SAT Engine (Plan 9 sonrası tek path) ─
            new() { Key = "Schedule.Engine", Value = "v2-ortools", Category = "Schedule", DataType = "string", Description = "Otomatik program çekirdeği. Plan 9 sonrası tek-path: 'v2-ortools' (Google OR-Tools CP-SAT). Eski 'v1-mrv' (MRV+SA+LNS) silindi." },
            new() { Key = "Schedule.OrTools.TimeLimitSeconds", Value = "30", Category = "Schedule", DataType = "int", Description = "CP-SAT max çözüm süresi (saniye). Bu süre dolduğunda solver son bulduğu çözümü döner." },
            new() { Key = "Schedule.OrTools.NumWorkers", Value = "8", Category = "Schedule", DataType = "int", Description = "CP-SAT parallel portfolio search worker sayısı. CPU çekirdek sayınızdan fazla yapmak fayda vermez." },
            new() { Key = "Schedule.OrTools.RandomSeed", Value = "42", Category = "Schedule", DataType = "int", Description = "Determinizm için CP-SAT random seed. Aynı seed = aynı çözüm." },
            new() { Key = "Schedule.OrTools.LogToConsole", Value = "false", Category = "Schedule", DataType = "bool", Description = "CP-SAT search progress'i console'a yaz (geliştirme sırasında true)." },
            new() { Key = "Schedule.OrTools.SearchStrategy", Value = "auto", Category = "Schedule", DataType = "string", Description = "CP-SAT search strategy: 'auto', 'fixed', 'portfolio'." },
            new() { Key = "Schedule.OrTools.UnsatCoreEnabled", Value = "true", Category = "Schedule", DataType = "bool", Description = "Infeasible çözümde UNSAT core extraction yap. Performance penalty küçük, default true." },

            // ── Algoritma Parametreleri (eski SchedulingAlgorithmConfigs'tan merge — 2026-05-09) ─
            // Hard threshold flag'leri — admin panel'inden toggle edilir, solver SystemConfigs'i okur.
            new() { Key = "Schedule.LunchBreak.IsHard", Value = "true", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: öğle arasına ders koyulamaz (zorunlu). KAPALI: koyulabilir ama ceza alır (tercihli)." },
            new() { Key = "Schedule.AmphiThresholdEnabled", Value = "false", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: kontenjan ≥ amfi eşiği olan derslere amfi zorunlu (H4). KAPALI: kural devre dışı (varsayılan — amfi envanteri olmayan ortamda UNSAT'i önler)." },
            new() { Key = "Schedule.ClassroomTypeRequirementEnabled", Value = "false", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: dersin RequiredTheoryClassroomType / RequiredLabClassroomType alanları zorunlu (H4). KAPALI: kural devre dışı." },
            new() { Key = "Schedule.AllowSameCourseSectionOverlap", Value = "false", Category = "HardThreshold", DataType = "bool", Description = "AÇIK: aynı dersin farklı şubeleri aynı slotta paralel açılabilir (H8 kapalı). KAPALI (varsayılan): şubeler farklı slotta olmalı." },

            // V1 Simulated Annealing parametreleri — V1 engine silindi (Plan 9), bu key'ler şu an atıl
            // ama UI gösteriyor. Cleanup PR'da silinecek.
            new() { Key = "Schedule.SA.MaxIterations", Value = "10000", Category = "SA", DataType = "int", Description = "Motor en fazla kaç iyileştirme denemesi yapsın. Artırırsan daha iyi program, ama daha yavaş çalışır. Öneri: 10000." },
            new() { Key = "Schedule.SA.InitialTemperature", Value = "100", Category = "SA", DataType = "int", Description = "Motorun başlangıçta ne kadar cesur olacağı (geniş arama). Zamanla soğur ve daha titiz olur. Öneri: 100." },
            new() { Key = "Schedule.SA.TimeoutSeconds", Value = "90", Category = "SA", DataType = "int", Description = "Motor en fazla kaç saniye çalışsın. Süre dolunca elindeki en iyi programı verir. Öneri: 90." },
            new() { Key = "Schedule.SA.StagnationLimit", Value = "2000", Category = "SA", DataType = "int", Description = "Motor kaç deneme üst üste iyileşme sağlamazsa sıkışmış sayılıp durur. Öneri: 2000." },
            new() { Key = "Schedule.SA.BacktrackLimit", Value = "50", Category = "SA", DataType = "int", Description = "İlk program kurulurken (Construction Solver) kaç kez geri alıp tekrar denenebilir. Öneri: 50." },
            new() { Key = "Schedule.SA.RelocateWeight", Value = "50", Category = "SA", DataType = "int", Description = "Motor 'tek bir dersin saatini değiştir' hamlesini ne sıklıkla denesin." },
            new() { Key = "Schedule.SA.SwapWeight", Value = "35", Category = "SA", DataType = "int", Description = "Motor 'iki dersin yerini değiştir' hamlesini ne sıklıkla denesin." },
            new() { Key = "Schedule.SA.ChainWeight", Value = "15", Category = "SA", DataType = "int", Description = "Motor 'zincirleme yer değiştir' (3+ ders aynı anda kaydır) hamlesini ne sıklıkla denesin." },

            new() { Key = "Schedule.LNS.MaxIterations", Value = "100", Category = "LNS", DataType = "int", Description = "Yan motor (yerel optimum kaçışı) en fazla kaç deneme yapsın." },
            new() { Key = "Schedule.LNS.TimeoutSeconds", Value = "30", Category = "LNS", DataType = "int", Description = "Yan motor en fazla kaç saniye çalışsın." },
            new() { Key = "Schedule.LNS.DestroySize", Value = "8", Category = "LNS", DataType = "int", Description = "Yan motor sıkışınca kaç dersin programını yıkıp baştan çözsün." },

            new() { Key = "Schedule.MRV.ClassroomWeight", Value = "3", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: derslik bulması zor olan dersleri ne kadar öne al." },
            new() { Key = "Schedule.MRV.SlotWeight", Value = "2", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: zaman dilimi seçeneği az olan dersleri ne kadar öne al." },
            new() { Key = "Schedule.MRV.LabWeight", Value = "2", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: lab gerektiren dersleri ne kadar öne al." },
            new() { Key = "Schedule.MRV.CapacityWeight", Value = "1", Category = "MRV", DataType = "int", Description = "İlk programı kurarken: kontenjanı yüksek dersleri ne kadar öne al." },

            new() { Key = "Scheduling.MaxBacktrackingDepth", Value = "3", Category = "Other", DataType = "int", Description = "Motor takıldığında kaç adım geri gidip tekrar denesin." },
            new() { Key = "Schedule.SplittableBlockTypes", Value = "Lab", Category = "Other", DataType = "string", Description = "Bir derslik yetmediğinde hangi tip dersler aynı saatte iki ayrı dersliğe bölünebilir." },
            new() { Key = "Schedule.Construction.SoftLookaheadCount", Value = "10", Category = "Other", DataType = "int", Description = "İlk programı kurarken her ders için kaç aday yer puanlansın." },
            new() { Key = "Schedule.Candidate.MaxCombinations", Value = "500", Category = "Other", DataType = "int", Description = "Bir ders için en fazla kaç saat/derslik kombinasyonu test edilsin." },
            new() { Key = "Schedule.Candidate.MaxWindowsPerDay", Value = "10", Category = "Other", DataType = "int", Description = "Bir derslik bir günde en fazla kaç saat aralığı önersin." },
            new() { Key = "Schedule.Candidate.MaxClassroomsPerType", Value = "25", Category = "Other", DataType = "int", Description = "Bir ders için en fazla kaç farklı derslik aday olarak değerlendirilsin." },
            new() { Key = "Schedule.Lab.MaxBlockHours", Value = "2", Category = "Other", DataType = "int", Description = "Lab dersi kaç saatlik bloklara bölünsün." },
            new() { Key = "Schedule.CurriculumConflictMode", Value = "cohort", Category = "Other", DataType = "string", Description = "H5 müfredat çakışma kontrolü modu. 'cohort' önerilen — section'ların alternatif olduğunu bilir." },

            new() { Key = "SoftConstraint.S20.PenaltyPerPair", Value = "5.0", Category = "Soft", DataType = "decimal", Description = "S20: Komşu yıl ders çiftinde modulo eşi çakışırsa pair başına eklenen ceza puanı." },
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
