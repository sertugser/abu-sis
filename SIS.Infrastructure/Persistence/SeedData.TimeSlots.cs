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
        var activeTerm = await context.AcademicTerms.FirstOrDefaultAsync(t => t.Status == TermStatus.Ready);
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
        var configs = await context.SystemConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);

        var slotDuration = GetConfigInt(configs, "Schedule.SlotDurationMinutes", 50);
        var breakMinutes = GetConfigInt(configs, "Schedule.BreakBetweenSlotsMinutes", 5);
        var morningStart = GetConfigTime(configs, "Schedule.MorningStartTime", new TimeOnly(9, 0));
        var lunchStart = GetConfigTime(configs, "Schedule.LunchBreakStart", new TimeOnly(11, 40));
        var lunchEnd = GetConfigTime(configs, "Schedule.LunchBreakEnd", new TimeOnly(12, 20));
        var eveningStart = GetConfigTime(configs, "Schedule.EveningStartTime", new TimeOnly(20, 10));
        var eveningSlotCount = GetConfigInt(configs, "Schedule.EveningSlotCount", 2);
        var workDaysStr = GetConfigString(configs, "Schedule.WorkDays", "Monday,Tuesday,Wednesday,Thursday,Friday");

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

                slots.Add(new TimeSlot
                {
                    TermId = termId,
                    DayOfWeek = day,
                    StartTime = current,
                    EndTime = end,
                    SlotOrder = slotOrder,
                    DisplayName = $"{GetDayName(day)} {slotOrder}. Ders ({current:HH:mm}-{end:HH:mm})"
                });

                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }

            // Öğleden sonra slotları: lunchEnd → akşam başlangıcına kadar
            current = lunchEnd;
            while (true)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));
                // Makul sınır: 18:30'dan sonra normal ders olmasın
                if (end > new TimeOnly(18, 30)) break;

                slots.Add(new TimeSlot
                {
                    TermId = termId,
                    DayOfWeek = day,
                    StartTime = current,
                    EndTime = end,
                    SlotOrder = slotOrder,
                    DisplayName = $"{GetDayName(day)} {slotOrder}. Ders ({current:HH:mm}-{end:HH:mm})"
                });

                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }

            // Akşam slotları
            current = eveningStart;
            for (var i = 0; i < eveningSlotCount; i++)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));

                slots.Add(new TimeSlot
                {
                    TermId = termId,
                    DayOfWeek = day,
                    StartTime = current,
                    EndTime = end,
                    SlotOrder = slotOrder,
                    DisplayName = $"{GetDayName(day)} {slotOrder}. Ders ({current:HH:mm}-{end:HH:mm})"
                });

                slotOrder++;
                current = end.Add(TimeSpan.FromMinutes(breakMinutes));
            }
        }

        return slots;
    }

    private static int GetConfigInt(Dictionary<string, string> configs, string key, int fallback) =>
        configs.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    private static TimeOnly GetConfigTime(Dictionary<string, string> configs, string key, TimeOnly fallback) =>
        configs.TryGetValue(key, out var v) && TimeOnly.TryParse(v, out var t) ? t : fallback;

    private static string GetConfigString(Dictionary<string, string> configs, string key, string fallback) =>
        configs.TryGetValue(key, out var v) ? v : fallback;

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
