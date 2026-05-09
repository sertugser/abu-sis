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
        var allMatchedSlots = new List<(TimeSlot timeSlot, int? classroomId)>();
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
                allMatchedSlots.Add((ts, effectiveClassroomId));
        }

        // Validate slot count matches course hours (defense in depth — solver should already enforce).
        var courseHours = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.Id == sectionId)
            .Select(cs => new { cs.Course.TheoryHours, cs.Course.LabHours })
            .FirstOrDefaultAsync(ct);

        var theoryHours = courseHours?.TheoryHours ?? 0;
        var labHours = courseHours?.LabHours ?? 0;
        var expectedSlotCount = theoryHours + labHours;
        if (expectedSlotCount > 0 && allMatchedSlots.Count != expectedSlotCount)
        {
            throw new InvalidOperationException(
                $"Slot sayısı uyumsuz: section {sectionId} için {expectedSlotCount} slot gerekli, {allMatchedSlots.Count} slot eşleşti.");
        }

        // H1 (classroom no-overlap) defensive check: V2 encoder enforces this in the solver,
        // but apply path may be invoked manually. Skip when constraint is disabled or non-Hard.
        var enforceClassroomConflict = await IsConstraintHardAsync("H1", defaultIsHard: true, ct);

        var slotIndex = 0;
        foreach (var (timeSlot, effectiveClassroomId) in allMatchedSlots)
        {
            if (enforceClassroomConflict && effectiveClassroomId.HasValue)
            {
                var alreadyOccupied = await _context.SectionSchedules
                    .AnyAsync(ss => ss.ClassroomId == effectiveClassroomId.Value
                        && ss.TimeSlotId == timeSlot.Id
                        && ss.CourseSectionId != sectionId, ct);

                if (alreadyOccupied)
                {
                    throw new InvalidOperationException(
                        $"Derslik çakışması: {DayLabel(timeSlot.DayOfWeek)} {timeSlot.StartTime:HH\\:mm}-{timeSlot.EndTime:HH\\:mm} slotunda derslik zaten dolu.");
                }
            }

            // BlockType heuristic: first TheoryHours slots → Theory, rest → Lab.
            // Solver/scheduler emits theory blocks before lab blocks, so slot order matches.
            var blockType = slotIndex < theoryHours
                ? ScheduleBlockType.Theory
                : ScheduleBlockType.Lab;

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

    private async Task<bool> IsConstraintHardAsync(string code, bool defaultIsHard, CancellationToken ct)
    {
        var definition = await _context.ConstraintDefinitions
            .AsNoTracking()
            .Where(d => d.Code == code)
            .Select(d => new { d.IsEnabled, d.Category })
            .FirstOrDefaultAsync(ct);

        if (definition == null)
            return defaultIsHard;
        if (!definition.IsEnabled)
            return false;
        return string.Equals(definition.Category, "Hard", StringComparison.OrdinalIgnoreCase);
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
