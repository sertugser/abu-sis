using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class TimeSlotService : ITimeSlotService
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;

    public TimeSlotService(AppDbContext context, ISystemConfigProvider systemConfig)
    {
        _context = context;
        _systemConfig = systemConfig;
    }

    public async Task<int> GenerateTimeSlotsForTermAsync(int termId, CancellationToken ct = default)
    {
        if (await _context.TimeSlots.AnyAsync(ts => ts.TermId == termId, ct))
            return 0;

        var slots = await SeedData.GenerateTimeSlotsFromConfigAsync(_context, termId);
        if (slots.Count == 0) return 0;

        _context.TimeSlots.AddRange(slots);
        await _context.SaveChangesAsync(ct);
        return slots.Count;
    }

    public async Task<IReadOnlyList<TimeSlotDto>> GetByTermAsync(int termId, CancellationToken ct = default)
    {
        var slots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .ToListAsync(ct);

        return slots.Select(ToDto).ToList();
    }

    public async Task<TimeSlotDto> CreateAsync(CreateTimeSlotDto dto, CancellationToken ct = default)
    {
        await EnsureTermEditableAsync(dto.TermId, ct);
        ValidateTimeRange(dto.StartTime, dto.EndTime);
        await ValidateConfiguredRulesAsync(dto.DayOfWeek, dto.StartTime, dto.EndTime, ct);
        await EnsureNoOverlapAsync(dto.TermId, dto.DayOfWeek, dto.StartTime, dto.EndTime, excludeSlotId: null, ct);

        var slotOrder = await _context.TimeSlots
            .Where(ts => ts.TermId == dto.TermId && ts.DayOfWeek == dto.DayOfWeek)
            .CountAsync(ct) + 1;

        var slot = new TimeSlot
        {
            TermId = dto.TermId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            SlotOrder = slotOrder,
            DisplayName = BuildDisplayName(dto.DayOfWeek, slotOrder, dto.StartTime, dto.EndTime),
            AllowedMode = dto.AllowedMode
        };

        _context.TimeSlots.Add(slot);
        await _context.SaveChangesAsync(ct);
        await ResequenceSlotOrdersAsync(dto.TermId, dto.DayOfWeek, ct);

        return ToDto(slot);
    }

    public async Task<TimeSlotDto> UpdateAsync(int id, UpdateTimeSlotDto dto, CancellationToken ct = default)
    {
        var slot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == id, ct)
            ?? throw new KeyNotFoundException($"TimeSlot {id} bulunamadı.");

        await EnsureTermEditableAsync(slot.TermId, ct);
        ValidateTimeRange(dto.StartTime, dto.EndTime);
        await ValidateConfiguredRulesAsync(dto.DayOfWeek, dto.StartTime, dto.EndTime, ct);
        await EnsureNoOverlapAsync(slot.TermId, dto.DayOfWeek, dto.StartTime, dto.EndTime, excludeSlotId: id, ct);

        var dayChanged = slot.DayOfWeek != dto.DayOfWeek;
        var oldDay = slot.DayOfWeek;

        slot.DayOfWeek = dto.DayOfWeek;
        slot.StartTime = dto.StartTime;
        slot.EndTime = dto.EndTime;
        slot.AllowedMode = dto.AllowedMode;
        slot.DisplayName = BuildDisplayName(dto.DayOfWeek, slot.SlotOrder, dto.StartTime, dto.EndTime);

        await _context.SaveChangesAsync(ct);

        if (dayChanged)
        {
            await ResequenceSlotOrdersAsync(slot.TermId, oldDay, ct);
            await ResequenceSlotOrdersAsync(slot.TermId, dto.DayOfWeek, ct);
        }
        else
        {
            await ResequenceSlotOrdersAsync(slot.TermId, dto.DayOfWeek, ct);
        }

        var refreshed = await _context.TimeSlots.AsNoTracking().FirstAsync(ts => ts.Id == id, ct);
        return ToDto(refreshed);
    }

    public async Task<TimeSlotDeleteResultDto> DeleteAsync(int id, bool force, CancellationToken ct = default)
    {
        var slot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == id, ct)
            ?? throw new KeyNotFoundException($"TimeSlot {id} bulunamadı.");

        await EnsureTermEditableAsync(slot.TermId, ct);

        var dependentCount = await _context.SectionSchedules
            .CountAsync(ss => ss.TimeSlotId == id, ct);

        if (dependentCount > 0 && !force)
        {
            return new TimeSlotDeleteResultDto(
                id,
                dependentCount,
                Deleted: false,
                Message: $"Bu slota bağlı {dependentCount} ders programı kaydı var. Silmek için onay gerekir.");
        }

        if (dependentCount > 0)
        {
            var schedules = await _context.SectionSchedules.Where(ss => ss.TimeSlotId == id).ToListAsync(ct);
            _context.SectionSchedules.RemoveRange(schedules);
        }

        var termId = slot.TermId;
        var day = slot.DayOfWeek;

        _context.TimeSlots.Remove(slot);
        await _context.SaveChangesAsync(ct);
        await ResequenceSlotOrdersAsync(termId, day, ct);

        return new TimeSlotDeleteResultDto(id, dependentCount, Deleted: true, Message: null);
    }

    public async Task<RegenerateTimeSlotsResultDto> RegenerateForTermAsync(int termId, bool force, CancellationToken ct = default)
    {
        await EnsureTermEditableAsync(termId, ct);

        var dependentCount = await _context.SectionSchedules
            .Where(ss => ss.TimeSlot.TermId == termId)
            .CountAsync(ct);

        if (dependentCount > 0 && !force)
        {
            return new RegenerateTimeSlotsResultDto(
                termId,
                GeneratedCount: 0,
                DependentScheduleCount: dependentCount,
                Regenerated: false,
                Message: $"Bu dönemde {dependentCount} ders programı kaydı var. Yeniden üretmek için onay gerekir (mevcut atamalar silinir).");
        }

        if (dependentCount > 0)
        {
            var schedules = await _context.SectionSchedules
                .Where(ss => ss.TimeSlot.TermId == termId)
                .ToListAsync(ct);
            _context.SectionSchedules.RemoveRange(schedules);
        }

        var existing = await _context.TimeSlots.Where(ts => ts.TermId == termId).ToListAsync(ct);
        _context.TimeSlots.RemoveRange(existing);
        await _context.SaveChangesAsync(ct);

        var newSlots = await SeedData.GenerateTimeSlotsFromConfigAsync(_context, termId);
        _context.TimeSlots.AddRange(newSlots);
        await _context.SaveChangesAsync(ct);

        return new RegenerateTimeSlotsResultDto(
            termId,
            GeneratedCount: newSlots.Count,
            DependentScheduleCount: dependentCount,
            Regenerated: true,
            Message: null);
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task EnsureTermEditableAsync(int termId, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Dönem {termId} bulunamadı.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde slot düzenlenemez.");
    }

    private static void ValidateTimeRange(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
            throw new ArgumentException("Bitiş saati başlangıçtan sonra olmalı.");
    }

    private async Task ValidateConfiguredRulesAsync(DayOfWeek day, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        var allowedDays = await GetConfiguredWorkDaysAsync(ct);
        if (!allowedDays.Contains(day))
            throw new InvalidOperationException($"'{GetDayName(day)}' günü sistemde ders günü olarak tanımlı değil.");

        var configuredDuration = await _systemConfig.GetIntAsync("Schedule.SlotDurationMinutes", 50, ct);
        var actualDuration = (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;
        if (actualDuration != configuredDuration)
            throw new InvalidOperationException($"Slot süresi sistem ayarı gereği tam {configuredDuration} dakika olmalıdır.");

        var lunchBreakIsHard = await _systemConfig.GetBoolAsync("Schedule.LunchBreak.IsHard", false, ct);
        if (!lunchBreakIsHard)
            return;

        var lunchStart = await GetConfiguredTimeAsync("Schedule.LunchBreakStart", new TimeOnly(11, 40), ct);
        var lunchEnd = await GetConfiguredTimeAsync("Schedule.LunchBreakEnd", new TimeOnly(12, 20), ct);

        if (start < lunchEnd && end > lunchStart)
            throw new InvalidOperationException($"Öğle arası sistem ayarı gereği {lunchStart:HH:mm}-{lunchEnd:HH:mm} aralığında slot tanımlanamaz.");
    }

    private async Task EnsureNoOverlapAsync(
        int termId, DayOfWeek day, TimeOnly start, TimeOnly end,
        int? excludeSlotId, CancellationToken ct)
    {
        var overlap = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId && ts.DayOfWeek == day)
            .Where(ts => excludeSlotId == null || ts.Id != excludeSlotId)
            .AnyAsync(ts => ts.StartTime < end && ts.EndTime > start, ct);

        if (overlap)
            throw new InvalidOperationException($"Aynı gün içinde {start:HH:mm}-{end:HH:mm} aralığıyla çakışan bir slot zaten var.");
    }

    private async Task ResequenceSlotOrdersAsync(int termId, DayOfWeek day, CancellationToken ct)
    {
        var slots = await _context.TimeSlots
            .Where(ts => ts.TermId == termId && ts.DayOfWeek == day)
            .OrderBy(ts => ts.StartTime)
            .ToListAsync(ct);

        for (var i = 0; i < slots.Count; i++)
        {
            var expectedOrder = i + 1;
            if (slots[i].SlotOrder == expectedOrder) continue;

            slots[i].SlotOrder = expectedOrder;
            slots[i].DisplayName = BuildDisplayName(day, expectedOrder, slots[i].StartTime, slots[i].EndTime);
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);
    }

    private static string BuildDisplayName(DayOfWeek day, int slotOrder, TimeOnly start, TimeOnly end) =>
        $"{GetDayName(day)} {slotOrder}. Ders ({start:HH:mm}-{end:HH:mm})";

    private async Task<HashSet<DayOfWeek>> GetConfiguredWorkDaysAsync(CancellationToken ct)
    {
        var raw = await _systemConfig.GetStringAsync(
            "Schedule.WorkDays",
            "Monday,Tuesday,Wednesday,Thursday,Friday",
            ct);

        var result = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => Enum.TryParse<DayOfWeek>(token, ignoreCase: true, out var parsed) ? parsed : (DayOfWeek?)null)
            .Where(day => day.HasValue)
            .Select(day => day!.Value)
            .ToHashSet();

        return result.Count == 0
            ? new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            }
            : result;
    }

    private async Task<TimeOnly> GetConfiguredTimeAsync(string key, TimeOnly fallback, CancellationToken ct)
    {
        var raw = await _systemConfig.GetStringAsync(key, fallback.ToString("HH:mm"), ct);
        return TimeOnly.TryParse(raw, out var parsed) ? parsed : fallback;
    }

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

    private static TimeSlotDto ToDto(TimeSlot s) => new(
        s.Id, s.TermId, s.DayOfWeek, s.StartTime, s.EndTime,
        s.SlotOrder, s.DisplayName, s.AllowedMode);
}
