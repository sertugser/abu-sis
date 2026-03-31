using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedTimeSlotsAsync(AppDbContext context)
    {
        if (await context.TimeSlots.AnyAsync()) return;

        var slots = new List<TimeSlot>
        {
            // MWF slotları (3×50dk) — Pazartesi, Çarşamba, Cuma
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(09, 00), EndTime = new(09, 50), SlotType = SlotType.Normal, DisplayName = "MWF 09:00-09:50" },
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(10, 00), EndTime = new(10, 50), SlotType = SlotType.Normal, DisplayName = "MWF 10:00-10:50" },
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(11, 00), EndTime = new(11, 50), SlotType = SlotType.Normal, DisplayName = "MWF 11:00-11:50" },
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(13, 00), EndTime = new(13, 50), SlotType = SlotType.Normal, DisplayName = "MWF 13:00-13:50" },
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(14, 00), EndTime = new(14, 50), SlotType = SlotType.Normal, DisplayName = "MWF 14:00-14:50" },
            new() { Pattern = TimeSlotPattern.MWF, StartTime = new(15, 00), EndTime = new(15, 50), SlotType = SlotType.Normal, DisplayName = "MWF 15:00-15:50" },

            // TR slotları (2×75dk) — Salı, Perşembe
            new() { Pattern = TimeSlotPattern.TR, StartTime = new(09, 00), EndTime = new(10, 15), SlotType = SlotType.Normal, DisplayName = "TR 09:00-10:15" },
            new() { Pattern = TimeSlotPattern.TR, StartTime = new(10, 30), EndTime = new(11, 45), SlotType = SlotType.Normal, DisplayName = "TR 10:30-11:45" },
            new() { Pattern = TimeSlotPattern.TR, StartTime = new(13, 00), EndTime = new(14, 15), SlotType = SlotType.Normal, DisplayName = "TR 13:00-14:15" },
            new() { Pattern = TimeSlotPattern.TR, StartTime = new(14, 30), EndTime = new(15, 45), SlotType = SlotType.Normal, DisplayName = "TR 14:30-15:45" },

            // Lab slotları (tek gün, 3 saatlik blok)
            new() { Pattern = TimeSlotPattern.SingleDay, StartTime = new(09, 00), EndTime = new(12, 00), SlotType = SlotType.Lab, DisplayName = "Lab 09:00-12:00" },
            new() { Pattern = TimeSlotPattern.SingleDay, StartTime = new(13, 00), EndTime = new(16, 00), SlotType = SlotType.Lab, DisplayName = "Lab 13:00-16:00" },
            new() { Pattern = TimeSlotPattern.SingleDay, StartTime = new(14, 00), EndTime = new(17, 00), SlotType = SlotType.Lab, DisplayName = "Lab 14:00-17:00" },
        };

        context.TimeSlots.AddRange(slots);
        await context.SaveChangesAsync();
    }
}
