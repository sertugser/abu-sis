using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record SolverRequest
{
    public required int TermId { get; init; }
    public required int FacultyId { get; init; }
    public int? DepartmentId { get; init; }
    public int? Seed { get; init; }
    public IReadOnlyList<SectionInput> Sections { get; init; } = [];
    public IReadOnlyList<ClassroomInput> Classrooms { get; init; } = [];
    public IReadOnlyList<TimeSlotInput> TimeSlots { get; init; } = [];
    public IReadOnlyDictionary<string, bool> ConstraintFlags { get; init; } =
        new Dictionary<string, bool>();
    public IReadOnlyList<CourseRelationship> Corequisites { get; init; } = [];

    /// <summary>
    /// Plan 5 — SystemConfig.Schedule.DefaultInstructorMaxWeeklyHours'tan okunur (default 30).
    /// SectionInput.InstructorMaxWeeklyHours == 0 olan section'lar için fallback.
    /// </summary>
    public int DefaultInstructorMaxWeeklyHours { get; init; } = 30;
}
