using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

/// <summary>
/// Single-responsibility writer for `SectionSchedule` records (the authoritative schedule store).
/// Consolidates two previously duplicated implementations
/// (`SchedulingService.WriteSectionScheduleRecordsAsync` + `FacultyPlanningService.WriteSectionScheduleRecordsAsync`)
/// to prevent silent divergence (BlockType bug 2026-05-10 was caused by such divergence).
/// </summary>
public interface IScheduleWriter
{
    /// <summary>
    /// Replaces all existing `SectionSchedule` records for the section with new ones derived
    /// from the supplied <paramref name="scheduleSlots"/>. Idempotent: previous records are
    /// removed first, then writes happen in a single transaction.
    /// </summary>
    /// <remarks>
    /// Behaviour notes:
    /// <list type="bullet">
    ///   <item>BlockType is assigned via heuristic — first <c>Course.TheoryHours</c> matched
    ///   slots are <c>Theory</c>, the remainder are <c>Lab</c> (solver/scheduler emits
    ///   theory-before-lab order).</item>
    ///   <item>If <see cref="Domain.Entities.ConstraintDefinition"/> H1 is enabled and Hard,
    ///   any classroom double-booking is treated as a hard conflict and an
    ///   <see cref="System.InvalidOperationException"/> is thrown (caller transaction is
    ///   expected to roll back). Online sections (no classroom) skip the check.</item>
    ///   <item>If a slot's <see cref="ScheduleSlotDto.ClassroomId"/> is set, it overrides the
    ///   section-level <paramref name="defaultClassroomId"/> for that slot only.</item>
    ///   <item>Total slot count must equal <c>TheoryHours + LabHours</c> for the course;
    ///   mismatch throws.</item>
    /// </list>
    /// </remarks>
    /// <param name="sectionId">CourseSection.Id whose schedule is being (re)written.</param>
    /// <param name="termId">Term scope; only TimeSlots in this term are matched.</param>
    /// <param name="defaultClassroomId">Section-level classroom (may be overridden per-slot).
    /// Null is allowed for online sections.</param>
    /// <param name="scheduleSlots">Per-slot day/time ranges with optional classroom override.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteAsync(
        int sectionId,
        int termId,
        int? defaultClassroomId,
        IReadOnlyList<ScheduleSlotDto> scheduleSlots,
        CancellationToken ct = default);
}
