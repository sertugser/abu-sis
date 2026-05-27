using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Single canonical implementation of <see cref="IScheduleWriter"/>.
/// 2026-05-10 — Replaces the two pre-existing private duplicates in SchedulingService and
/// FacultyPlanningService. The BlockType bug surfaced because FacultyPlanningService skipped
/// BlockType assignment while SchedulingService set it; consolidation eliminates that class
/// of bug going forward.
/// </summary>
public sealed class ScheduleWriter : IScheduleWriter
{
    private readonly AppDbContext _context;

    public ScheduleWriter(AppDbContext context)
    {
        _context = context;
    }

    public async Task WriteAsync(
        int sectionId,
        int termId,
        int? defaultClassroomId,
        IReadOnlyList<ScheduleSlotDto> scheduleSlots,
        CancellationToken ct = default)
    {
        // Always clear existing records first (idempotency).
        var existing = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(ct);

        if (existing.Count > 0)
            _context.SectionSchedules.RemoveRange(existing);

        if (scheduleSlots.Count == 0)
            return;

        // Load TimeSlots in the term once.
        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(ct);

        // Resolve which academic time slots are covered by the requested ranges,
        // preserving day/time-ordered emission so the BlockType heuristic
        // (theory-first, lab-second) remains correct.
        var allMatchedSlots = new List<(TimeSlot timeSlot, int? classroomId, ScheduleBlockType? blockType)>();
        foreach (var slot in scheduleSlots)
        {
            var slotDay = (DayOfWeek)slot.Day;
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slotDay
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            if (matchingTimeSlots.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{DayLabel(slotDay)} {slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm} aralığı dönem zaman slotlarıyla eşleşmiyor.");
            }

            // Per-slot classroom override (Online sections may have null at both levels).
            var effectiveClassroomId = slot.ClassroomId ?? defaultClassroomId;
            foreach (var ts in matchingTimeSlots)
                allMatchedSlots.Add((ts, effectiveClassroomId, slot.BlockType));
        }

        // Slot sayısı (theory+lab) kontrolü kaldırıldı — V2 RuleEngine H12_CreditHourRule aynı
        // kontrolü yapar ve Force=true ile override edilebilir. ScheduleWriter sadece "yaz"
        // sorumluluğuna odaklanır.

        // H1 defansif kontrolü kaldırıldı — V2 RuleEngine H1_ClassroomConflictRule aynı
        // kontrolü yapıyor; Force=false ile çağrıldığında AssignSectionAsync conflict
        // listesini dialog'a yansıtır, Force=true ile override edilirse yazılır. Defansif
        // check Force akışını blokluyordu.

        // TheoryHours — BlockType heuristik için (ilk N slot Theory, gerisi Lab).
        var theoryHours = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.Id == sectionId)
            .Select(cs => cs.Course.TheoryHours)
            .FirstOrDefaultAsync(ct);

        var slotIndex = 0;
        foreach (var (timeSlot, effectiveClassroomId, explicitBlockType) in allMatchedSlots)
        {
            // Açık blockType sağlanmışsa (düzenleyici teori/lab'ı ayrı sürükler) onu yaz; aksi halde
            // pozisyonel heuristik (ilk TheoryHours slot → Theory). Açık değer, lab bloğu teori'den
            // önce atandığında yanlış etiketlenmesini önler (sıra-bağımlı tahmin sorunu).
            var blockType = explicitBlockType
                ?? (slotIndex < theoryHours ? ScheduleBlockType.Theory : ScheduleBlockType.Lab);

            _context.SectionSchedules.Add(new SectionSchedule
            {
                CourseSectionId = sectionId,
                TimeSlotId = timeSlot.Id,
                ClassroomId = effectiveClassroomId,
                BlockType = blockType,
            });
            slotIndex++;
        }
    }

    private static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString(),
    };
}
