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

            // ── Plan 11 Faz 5 — RuleEngine her zaman aktif (V1 evaluator silindi 2026-05-15).
            // Schedule.UseV2RuntimeRules flag'i artık gereksiz; RemoveUseV2RuntimeRulesFlag
            // migration ile mevcut DB'lerden de düşürüldü.

            // ── Algoritma Parametreleri (eski SchedulingAlgorithmConfigs'tan merge — 2026-05-09) ─
            // 2026-05-19: 4 hard threshold flag'i (LunchBreak.IsHard, AllowSameCourseSectionOverlap,
            // AmphiThresholdEnabled, ClassroomTypeRequirementEnabled) kaldırıldı. Bu kuralların
            // açma/kapama + Hard/Soft toggle + alt-parametreleri tek otorite olarak
            // ConstraintDefinitions["H13"|"H8"|"H4"].Category + Parameters JSON'a (Kısıtlar sayfası)
            // taşındı. Algoritma Parametreleri sayfasında HardThreshold kategorisi artık boştur.

            // V1 silindiğinde 19 atıl key migration `RemoveAtilV1AlgorithmKeys` ile DB'den kaldırıldı
            // (2026-05-10). Bonus: aynı gün BatchAuto.cs ölü kod silinmesi sonrası
            // Scheduling.MaxBacktrackingDepth de migration `RemoveBatchAutoConfigKey` ile düştü.
            // Geriye kalan key'ler V2 scheduler tarafından hâlâ okunan canlı ayarlar.
            // Schedule.SplittableBlockTypes ve Schedule.Lab.MaxBlockHours kaldırıldı (2026-05-17):
            // hiçbir encoder/builder okumuyordu (atıl). DB temizliği migration ile yapıldı.
            // 2026-05-19: Schedule.MaxLabBlockHours kaldırıldı → H6.Parameters JSON tek otorite
            // (Kısıtlar sayfası → H6 "Ders Bileşen Çakışması" dialog).
            // 2026-05-19: Schedule.CurriculumConflictMode kaldırıldı → H5.Parameters JSON tek otorite.

            // SoftConstraint.S20.PenaltyPerPair kaldırıldı (2026-05-17): S20 Soft → Hard
            // dönüşümüyle ceza puanı anlamsızlaştı (encoder soft path'i çalışmıyor, hard
            // path zaten section-aware NoOverlap). DB temizliği migration ile yapıldı.

            // ── Akıllı Derslik Atama Önerisi (ClassroomSuggestionService) ─────
            new() { Key = "Classroom.AmphiThreshold", Value = "100", Category = "Suggestion", DataType = "int", Description = "Bir dersi amfi gerektiren öğrenci eşiği. Tahmini talep bu değerin üzerindeyse ders amfide verilmeli sayılır." },
            new() { Key = "Suggestion.ElectiveDemandRatio", Value = "0.35", Category = "Suggestion", DataType = "decimal", Description = "Seçmeli dersleri öğrencilerin kaç katı talep eder (0-1 arası). 0.35 = %35'i alır varsayımı." },
            new() { Key = "Suggestion.HistoricalWeight", Value = "0.6", Category = "Suggestion", DataType = "decimal", Description = "Tarihsel veri ile tahmin harmanlamasında tarihsel veriye verilen ağırlık (0-1). 0.6 = tarihsel %60, tahmin %40." },
            new() { Key = "Suggestion.HistoricalAnomalyCap", Value = "2.0", Category = "Suggestion", DataType = "decimal", Description = "Anomali koruması: tarihsel kullanım, tahminin bu katından fazlasını geçemez. 2.0 = max tahminin 2 katı." },
            new() { Key = "Suggestion.DemandWeight.Student", Value = "1.0", Category = "Suggestion", DataType = "decimal", Description = "Fakülte talep puanında öğrenci sayısı katsayısı." },
            new() { Key = "Suggestion.DemandWeight.Curriculum", Value = "3.0", Category = "Suggestion", DataType = "decimal", Description = "Fakülte talep puanında müfredat dersi sayısı katsayısı." },
            new() { Key = "Suggestion.DemandWeight.Lab", Value = "2.0", Category = "Suggestion", DataType = "decimal", Description = "Fakülte talep puanında laboratuvar dersi sayısı katsayısı." },

            // ── İyileştirilmiş Greedy parametreleri (faz arası feedback + fairness) ──
            new() { Key = "Suggestion.MinFairnessGuarantee", Value = "1", Category = "Suggestion", DataType = "int", Description = "Her aktif fakülteye normal sınıf fazında garantilenen minimum derslik sayısı (kıt kaynak adaleti). 0 = garanti yok." },
            new() { Key = "Suggestion.SoftThresholdBandPercent", Value = "10", Category = "Suggestion", DataType = "decimal", Description = "Amfi eşiğinin etrafındaki yumuşak bant (±%). Bu bandtaki dersler 'soft-amphi' olarak işaretlenir; tahminde gri bölgeyi gösterir." },
            new() { Key = "Suggestion.PhaseFeedbackEnabled", Value = "true", Category = "Suggestion", DataType = "bool", Description = "AÇIK: Amfi/lab dağıtımı sonrası fakültenin tatmin edilmiş ihtiyacı DemandScore'dan düşülür, normal sınıf payı buna göre yeniden hesaplanır." },
            new() { Key = "Suggestion.PhaseFeedbackWeight", Value = "0.5", Category = "Suggestion", DataType = "decimal", Description = "Faz arası feedback'in DemandScore'a etki oranı (0-1). 1.0 = tam düş, 0.0 = hiç düşme, 0.5 = yarısını düş (dengeli)." },
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
