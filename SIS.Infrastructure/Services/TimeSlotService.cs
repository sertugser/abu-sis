using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class TimeSlotService : ITimeSlotService
{
    private readonly AppDbContext _context;

    public TimeSlotService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GenerateTimeSlotsForTermAsync(int termId, CancellationToken ct = default)
    {
        // Zaten slot varsa tekrar oluşturma
        if (await _context.TimeSlots.AnyAsync(ts => ts.TermId == termId, ct))
            return 0;

        var slots = await SeedData.GenerateTimeSlotsFromConfigAsync(_context, termId);
        if (slots.Count == 0) return 0;

        _context.TimeSlots.AddRange(slots);
        await _context.SaveChangesAsync(ct);
        return slots.Count;
    }
}
