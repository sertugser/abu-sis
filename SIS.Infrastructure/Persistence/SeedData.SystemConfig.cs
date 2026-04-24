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

            // ── Denetim Kayıtları ─────────────────────────────────
            new() { Key = "Audit.RetentionDays", Value = "365", Category = "General", DataType = "int", Description = "Denetim kayıtlarının saklanma süresi (gün)" },

            // ── Ders Çekilme Limitleri ────────────────────────────
            new() { Key = "Withdrawal.MaxCount.Undergraduate", Value = "6", Category = "Registration", DataType = "int", Description = "Lisans öğrencileri için toplam çekilme hakkı" },
            new() { Key = "Withdrawal.MaxCount.Associate", Value = "3", Category = "Registration", DataType = "int", Description = "Önlisans öğrencileri için toplam çekilme hakkı" },

            // ── YÖK Unvan → MaxWeeklyHours ───────────────────────
            new() { Key = "Instructor.MaxWeeklyHours.Professor", Value = "12", Category = "Scheduling", DataType = "int", Description = "Profesör max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.AssociateProfessor", Value = "16", Category = "Scheduling", DataType = "int", Description = "Doçent max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.AssistantProfessor", Value = "20", Category = "Scheduling", DataType = "int", Description = "Dr. Öğr. Üyesi max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.Lecturer", Value = "24", Category = "Scheduling", DataType = "int", Description = "Öğretim Görevlisi max haftalık ders saati" },

            // ── Derslik Eşikleri ──────────────────────────────────
            new() { Key = "Classroom.AmphiThreshold", Value = "100", Category = "Scheduling", DataType = "int", Description = "Bu sayıdan fazla kontenjan → Amfi zorunlu" },
            new() { Key = "Classroom.OccupancyTarget.Min", Value = "60", Category = "Scheduling", DataType = "int", Description = "Derslik doluluk oranı alt hedefi (%)" },
            new() { Key = "Classroom.OccupancyTarget.Max", Value = "85", Category = "Scheduling", DataType = "int", Description = "Derslik doluluk oranı üst hedefi (%)" },

            // ── Soft Constraint Kontrolleri ───────────────────────
            new() { Key = "SoftConstraint.S1.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S1: Derslik doluluk oranı kontrolü aktif/pasif" },
            new() { Key = "SoftConstraint.S2.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S2: Hoca günlük yoğunluk dengesi kontrolü aktif/pasif" },
            new() { Key = "SoftConstraint.S2.MaxDailyHours", Value = "6", Category = "Scheduling", DataType = "int", Description = "S2: Bir hocanın günlük max ders saati" },
            new() { Key = "SoftConstraint.S3.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S3: Slot dağılım dengesi kontrolü aktif/pasif" },
            new() { Key = "SoftConstraint.S3.MaxSectionsPerSlot", Value = "5", Category = "Scheduling", DataType = "int", Description = "S3: Bir slot'ta max section sayısı (prime-time yığılma önleme)" },
            new() { Key = "SoftConstraint.S4.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S4: Cross-department sık birlikte alınan ders çakışma uyarısı aktif/pasif" },
            new() { Key = "SoftConstraint.S4.MinCoPairThreshold", Value = "20", Category = "Scheduling", DataType = "int", Description = "S4: Birlikte alınan ders çifti minimum öğrenci eşiği" },
            new() { Key = "SoftConstraint.S5.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S5: Öğle arası koruması (soft mod) aktif/pasif" },
            new() { Key = "SoftConstraint.S7.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S7: Öğrenci ders arası boşluk kontrolü aktif/pasif" },
            new() { Key = "SoftConstraint.S7.MaxGapSlots", Value = "3", Category = "Scheduling", DataType = "int", Description = "S7: Max ardışık boş slot sayısı" },
            new() { Key = "SoftConstraint.S8.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S8: Hoca gün sayısı minimizasyonu aktif/pasif" },
            new() { Key = "SoftConstraint.S9.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S9: Erken sabah/geç akşam cezası aktif/pasif" },
            new() { Key = "SoftConstraint.S12.Enabled", Value = "true", Category = "Scheduling", DataType = "bool", Description = "S12: Hafta içi dengeli dağılım aktif/pasif" },

            // ── Soft Constraint Ağırlıkları ──────────────────────
            new() { Key = "SoftConstraint.Weight.S1", Value = "3", Category = "Scheduling", DataType = "decimal", Description = "S1 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S2", Value = "2", Category = "Scheduling", DataType = "decimal", Description = "S2 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S3", Value = "2", Category = "Scheduling", DataType = "decimal", Description = "S3 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S4", Value = "3", Category = "Scheduling", DataType = "decimal", Description = "S4 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S5", Value = "2", Category = "Scheduling", DataType = "decimal", Description = "S5 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S7", Value = "2", Category = "Scheduling", DataType = "decimal", Description = "S7 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S8", Value = "1", Category = "Scheduling", DataType = "decimal", Description = "S8 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S9", Value = "1", Category = "Scheduling", DataType = "decimal", Description = "S9 ağırlığı" },
            new() { Key = "SoftConstraint.Weight.S12", Value = "1", Category = "Scheduling", DataType = "decimal", Description = "S12 ağırlığı" },

            // ── Scheduling Algoritma Parametreleri ───────────────
            new() { Key = "Scheduling.MaxBacktrackingDepth", Value = "3", Category = "Scheduling", DataType = "int", Description = "Backtracking max derinlik" },
            new() { Key = "Schedule.LunchBreak.IsHard", Value = "true", Category = "Schedule", DataType = "bool", Description = "Öğle arası hard constraint mi (true) soft constraint mi (false)" },
            new() { Key = "Schedule.MaxConsecutiveHours", Value = "4", Category = "Schedule", DataType = "int", Description = "Hocanın aralıksız max ardışık ders saati" },

            // ── Otomatik Programlama (SA) Parametreleri ───────────
            new() { Key = "Schedule.SA.MaxIterations", Value = "10000", Category = "Scheduling", DataType = "int", Description = "SA algoritması max iterasyon sayısı" },
            new() { Key = "Schedule.SA.InitialTemperature", Value = "100", Category = "Scheduling", DataType = "int", Description = "SA başlangıç sıcaklığı" },
            new() { Key = "Schedule.SA.TimeoutSeconds", Value = "90", Category = "Scheduling", DataType = "int", Description = "SA zaman aşımı (saniye)" },
            new() { Key = "Schedule.SA.StagnationLimit", Value = "2000", Category = "Scheduling", DataType = "int", Description = "SA durgunluk limiti (iyileşme olmayan iterasyon)" },
            new() { Key = "Schedule.SA.BacktrackLimit", Value = "50", Category = "Scheduling", DataType = "int", Description = "Construction solver backtrack limiti" },
            new() { Key = "Schedule.SA.RelocateWeight", Value = "50", Category = "Scheduling", DataType = "int", Description = "SA relocate hamlesi ağırlığı" },
            new() { Key = "Schedule.SA.SwapWeight", Value = "35", Category = "Scheduling", DataType = "int", Description = "SA swap hamlesi ağırlığı" },
            new() { Key = "Schedule.SA.ChainWeight", Value = "15", Category = "Scheduling", DataType = "int", Description = "SA chain-relocate hamlesi ağırlığı" },

            // ── MRV (Minimum Remaining Values) Ağırlıkları ────────
            new() { Key = "Schedule.MRV.ClassroomWeight", Value = "3", Category = "Scheduling", DataType = "int", Description = "MRV: derslik kısıtı ağırlığı" },
            new() { Key = "Schedule.MRV.SlotWeight", Value = "2", Category = "Scheduling", DataType = "int", Description = "MRV: zaman dilimi kısıtı ağırlığı" },
            new() { Key = "Schedule.MRV.LabWeight", Value = "2", Category = "Scheduling", DataType = "int", Description = "MRV: lab gereksinimi ağırlığı" },
            new() { Key = "Schedule.MRV.CapacityWeight", Value = "1", Category = "Scheduling", DataType = "int", Description = "MRV: kapasite kısıtı ağırlığı" },
            new() { Key = "Schedule.TheoryLabSplitThreshold", Value = "4", Category = "Scheduling", DataType = "int", Description = "Teori+lab toplamı bu değeri aşarsa ayrı günlere bölünür" },

            // ── Ders Programı Grid Parametreleri ─────────────────
            new() { Key = "Schedule.SlotDurationMinutes", Value = "50", Category = "Schedule", DataType = "int", Description = "Bir ders slotunun süresi (dakika)" },
            new() { Key = "Schedule.BreakBetweenSlotsMinutes", Value = "5", Category = "Schedule", DataType = "int", Description = "İki ders arası mola süresi (dakika)" },
            new() { Key = "Schedule.MorningStartTime", Value = "09:00", Category = "Schedule", DataType = "string", Description = "Sabah ilk ders başlangıç saati" },
            new() { Key = "Schedule.LunchBreakStart", Value = "11:40", Category = "Schedule", DataType = "string", Description = "Öğle arası başlangıç saati" },
            new() { Key = "Schedule.LunchBreakEnd", Value = "12:20", Category = "Schedule", DataType = "string", Description = "Öğle arası bitiş saati" },
            new() { Key = "Schedule.AfternoonEndTime", Value = "18:30", Category = "Schedule", DataType = "string", Description = "Öğleden sonra son dersin en geç bitiş saati" },
            new() { Key = "Schedule.EveningStartTime", Value = "20:10", Category = "Schedule", DataType = "string", Description = "Akşam dersleri başlangıç saati" },
            new() { Key = "Schedule.EveningSlotCount", Value = "2", Category = "Schedule", DataType = "int", Description = "Akşam ders slot sayısı" },
            new() { Key = "Schedule.WorkDays", Value = "Monday,Tuesday,Wednesday,Thursday,Friday", Category = "Schedule", DataType = "string", Description = "Ders yapılan günler (virgülle ayrılmış)" },
            new() { Key = "Schedule.FaceToFaceCutoffTime", Value = "18:00", Category = "Schedule", DataType = "string", Description = "Bu saatten sonraki TimeSlot'lar yeni oluşturulduğunda OnlineOnly damgalanır (ve FaceToFaceSlotRestriction evaluator'ı bu saatten sonrasında yüz-yüze ders kabul etmez)" },
            new() { Key = "Schedule.WeekendOnlineOnly", Value = "true", Category = "Schedule", DataType = "bool", Description = "Hafta sonu (Cts/Paz) slotları OnlineOnly damgalanır ve yüz-yüze ders kabul etmez" },
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
