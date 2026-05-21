using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    /// <summary>
    /// TimeSlot'lar artık dönem başında SystemConfig parametrelerinden generate edilir.
    /// Seed data olarak sadece aktif dönem varsa ve slotları yoksa oluşturulur.
    /// </summary>
    public static async Task SeedTimeSlotsAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var activeTerm = await context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.IsActivated && t.TermStart <= now && t.TermEnd >= now);
        if (activeTerm == null) return;

        if (await context.TimeSlots.AnyAsync(ts => ts.TermId == activeTerm.Id)) return;

        var slots = await GenerateTimeSlotsFromConfigAsync(context, activeTerm.Id);
        if (slots.Count > 0)
        {
            context.TimeSlots.AddRange(slots);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// SystemConfig parametrelerinden TimeSlot'ları generate eder.
    /// </summary>
    public static async Task<List<TimeSlot>> GenerateTimeSlotsFromConfigAsync(AppDbContext context, int termId)
    {
        // 2026-05-09: Tüm scheduling key'leri SystemConfigs'e merge edildi (eski dual-table fix).
        var configs = await context.SystemConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);

        var slotDuration = GetConfigInt(configs, "Schedule.SlotDurationMinutes", 50);
        var breakMinutes = GetConfigInt(configs, "Schedule.BreakBetweenSlotsMinutes", 5);
        var morningStart = GetConfigTime(configs, "Schedule.MorningStartTime", new TimeOnly(9, 0));
        var lunchStart = GetConfigTime(configs, "Schedule.LunchBreakStart", new TimeOnly(11, 40));
        var lunchEnd = GetConfigTime(configs, "Schedule.LunchBreakEnd", new TimeOnly(12, 20));
        var afternoonEnd = GetConfigTime(configs, "Schedule.AfternoonEndTime", new TimeOnly(18, 30));
        var eveningStart = GetConfigTime(configs, "Schedule.EveningStartTime", new TimeOnly(20, 10));
        var eveningSlotCount = GetConfigInt(configs, "Schedule.EveningSlotCount", 2);
        var workDaysStr = GetConfigString(configs, "Schedule.WorkDays", "Monday,Tuesday,Wednesday,Thursday,Friday");
        var faceToFaceCutoff = GetConfigTime(configs, "Schedule.FaceToFaceCutoffTime", new TimeOnly(18, 0));
        var weekendOnlineOnly = GetConfigBool(configs, "Schedule.WeekendOnlineOnly", true);

        var workDays = workDaysStr.Split(',')
            .Select(d => Enum.Parse<DayOfWeek>(d.Trim()))
            .ToList();

        var slots = new List<TimeSlot>();

        foreach (var day in workDays)
        {
            var slotOrder = 1;

            // Sabah slotları: morningStart → lunchStart
            var current = morningStart;
            while (true)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));
                if (end > lunchStart) break;

                slots.Add(BuildSlot(termId, day, current, end, slotOrder, faceToFaceCutoff, weekendOnlineOnly));
                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }

            // Öğleden sonra slotları: lunchEnd → afternoonEnd
            current = lunchEnd;
            while (true)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));
                if (end > afternoonEnd) break;

                slots.Add(BuildSlot(termId, day, current, end, slotOrder, faceToFaceCutoff, weekendOnlineOnly));
                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }

            // Akşam slotları
            current = eveningStart;
            for (var i = 0; i < eveningSlotCount; i++)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));

                slots.Add(BuildSlot(termId, day, current, end, slotOrder, faceToFaceCutoff, weekendOnlineOnly));
                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }
        }

        return slots;
    }

    /// <summary>
    /// TimeSlot inşa eder ve AllowedMode'u saat/gün kurallarına göre otomatik damgalar.
    /// Kural: StartTime >= faceToFaceCutoff VEYA hafta sonu günü → OnlineOnly; aksi halde Both.
    /// Admin sonradan elle değiştirebilir — bu sadece "generate anında varsayılan".
    /// </summary>
    private static TimeSlot BuildSlot(
        int termId,
        DayOfWeek day,
        TimeOnly start,
        TimeOnly end,
        int slotOrder,
        TimeOnly faceToFaceCutoff,
        bool weekendOnlineOnly)
    {
        var isWeekend = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
        var isPastCutoff = start >= faceToFaceCutoff;
        var allowedMode = (isPastCutoff || (weekendOnlineOnly && isWeekend))
            ? TimeSlotAllowedMode.OnlineOnly
            : TimeSlotAllowedMode.Both;

        return new TimeSlot
        {
            TermId = termId,
            DayOfWeek = day,
            StartTime = start,
            EndTime = end,
            SlotOrder = slotOrder,
            DisplayName = $"{GetDayName(day)} {slotOrder}. Ders ({start:HH:mm}-{end:HH:mm})",
            AllowedMode = allowedMode
        };
    }

    private static int GetConfigInt(Dictionary<string, string> configs, string key, int fallback) =>
        configs.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    private static TimeOnly GetConfigTime(Dictionary<string, string> configs, string key, TimeOnly fallback) =>
        configs.TryGetValue(key, out var v) && TimeOnly.TryParse(v, out var t) ? t : fallback;

    private static string GetConfigString(Dictionary<string, string> configs, string key, string fallback) =>
        configs.TryGetValue(key, out var v) ? v : fallback;

    private static bool GetConfigBool(Dictionary<string, string> configs, string key, bool fallback) =>
        configs.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static string GetDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString()
    };
}
